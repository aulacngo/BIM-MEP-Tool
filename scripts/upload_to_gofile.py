import os
import sys
import json
import time
import requests

ACCOUNT_FILE = os.path.join(os.path.dirname(__file__), "gofile_account.json")
RELEASES_FILE = os.path.join(os.path.dirname(__file__), "gofile_releases.json")

def get_or_create_account():
    if os.path.exists(ACCOUNT_FILE):
        try:
            with open(ACCOUNT_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass

    # Create new account
    try:
        resp = requests.post("https://api.gofile.io/accounts").json()
        if resp.get("status") == "ok":
            acc = resp["data"]
            with open(ACCOUNT_FILE, "w", encoding="utf-8") as f:
                json.dump(acc, f, indent=2)
            return acc
    except Exception as e:
        print(f"Warning: Failed to create Gofile account: {e}")
    return None

def load_releases():
    if os.path.exists(RELEASES_FILE):
        try:
            with open(RELEASES_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return []

def save_releases(releases):
    try:
        with open(RELEASES_FILE, "w", encoding="utf-8") as f:
            json.dump(releases, f, indent=2)
    except Exception as e:
        print(f"Warning: Failed to save releases history: {e}")

def delete_gofile_content(token, content_id):
    try:
        headers = {"Authorization": f"Bearer {token}"}
        resp = requests.delete(
            "https://api.gofile.io/contents",
            headers=headers,
            json={"contentsId": content_id}
        ).json()
        return resp.get("status") == "ok"
    except Exception as e:
        print(f"Failed to delete {content_id}: {e}")
        return False

def clean_old_gofile_files(token, keep_latest_id=None):
    releases = load_releases()
    remaining = []
    deleted_count = 0

    for r in releases:
        cid = r.get("id")
        if not cid:
            continue
        if keep_latest_id and cid == keep_latest_id:
            remaining.append(r)
            continue

        print(f"Deleting old Gofile file: {r.get('name', cid)} ({r.get('downloadPage', '')})...")
        if delete_gofile_content(token, cid):
            deleted_count += 1
            print(f"  -> Deleted successfully.")
        else:
            print(f"  -> File already removed or expired.")

    if keep_latest_id:
        save_releases(remaining)
    else:
        save_releases([])
    return deleted_count

def upload_to_gofile(file_path, auto_clean_old=True):
    if not os.path.exists(file_path):
        print(f"Error: File '{file_path}' not found.")
        sys.exit(1)

    file_size_mb = os.path.getsize(file_path) / (1024 * 1024)
    file_name = os.path.basename(file_path)
    print(f"Uploading '{file_name}' ({file_size_mb:.2f} MB) to Gofile...")

    acc = get_or_create_account()
    token = acc.get("token") if acc else None
    root_folder = acc.get("rootFolder") if acc else None

    # 1. Get available server
    print("Getting available Gofile server...")
    resp = requests.get("https://api.gofile.io/servers")
    resp_data = resp.json()
    if resp_data.get("status") != "ok":
        print(f"Failed to get server: {resp_data}")
        sys.exit(1)

    servers = resp_data["data"]["servers"]
    if not servers:
        print("No servers available.")
        sys.exit(1)

    server = servers[0]["name"]
    upload_url = f"https://{server}.gofile.io/contents/uploadfile"
    print(f"Selected server: {server}")
    print(f"Uploading file...")

    # 2. Upload file (no folderId so Gofile creates a 100% public download page)
    with open(file_path, "rb") as f:
        files = {"file": (file_name, f)}
        upload_resp = requests.post(upload_url, files=files)

    upload_data = upload_resp.json()
    if upload_data.get("status") != "ok":
        print(f"Upload failed: {upload_data}")
        sys.exit(1)

    data = upload_data.get("data", {})
    download_page = data.get("downloadPage")
    file_id = data.get("id")

    print("==================================================")
    print("UPLOAD SUCCESSFUL!")
    print(f"Download URL: {download_page}")
    print(f"File ID: {file_id}")
    print("==================================================")

    # 3. Track in releases
    releases = load_releases()
    releases.append({
        "id": file_id,
        "name": file_name,
        "downloadPage": download_page,
        "uploaded_at": time.strftime("%Y-%m-%d %H:%M:%S")
    })
    save_releases(releases)

    # 4. Clean older files if requested
    if auto_clean_old and token:
        clean_old_gofile_files(token, keep_latest_id=file_id)

    return download_page

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--clean-all":
        acc = get_or_create_account()
        if acc and acc.get("token"):
            count = clean_old_gofile_files(acc["token"])
            print(f"Cleaned {count} old Gofile files.")
        else:
            print("No Gofile account found.")
        sys.exit(0)

    target = sys.argv[1] if len(sys.argv) > 1 else r"D:\Tool Revit\release\BIM-Tool-CurrentUser-NoAdmin-20261001.zip"
    upload_to_gofile(target, auto_clean_old=True)
