import os
import sys
from google.oauth2.credentials import Credentials
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

# Muc tieu thu muc Google Drive co dinh theo yeu cau cua user
TARGET_FOLDER_ID = "1w5pArkruN_D5IWcKzlBfR_7bEDe8qVYr"

def upload_file(file_path):
    token_path = r"C:\Users\aulac\.google_workspace_mcp\credentials\drive_token.json"
    if not os.path.exists(token_path):
        print("Loi: Chua xac thuc Google Drive. Token khong ton tai tai: " + token_path)
        sys.exit(1)

    creds = Credentials.from_authorized_user_file(token_path)
    service = build('drive', 'v3', credentials=creds)

    filename = os.path.basename(file_path)
    print(f"Dang upload '{filename}' vao thu muc Drive: {TARGET_FOLDER_ID}...")

    # Kiem tra xem file da ton tai trong folder nay chua de update hoac create moi
    query = f"'{TARGET_FOLDER_ID}' in parents and name = '{filename}' and trashed = false"
    results = service.files().list(q=query, fields="files(id, name)").execute()
    items = results.get('files', [])

    media = MediaFileUpload(file_path, resumable=True)

    if items:
        # Neu file da co trong folder, update noi dung file cu
        file_id = items[0]['id']
        print(f"Phat hien file da ton tai (ID: {file_id}), dang ghi de/cap nhat...")
        file = service.files().update(fileId=file_id, media_body=media, fields='id, webViewLink, webContentLink').execute()
    else:
        # Tao file moi trong thu muc duoc chi dinh
        file_metadata = {
            'name': filename,
            'parents': [TARGET_FOLDER_ID]
        }
        file = service.files().create(body=file_metadata, media_body=media, fields='id, webViewLink, webContentLink').execute()
        file_id = file.get('id')

    # Get updated link
    f = service.files().get(fileId=file_id, fields='webViewLink, webContentLink').execute()
    view_link = f.get('webViewLink')

    print("\n=========================================================================")
    print("  UPLOAD GOOGLE DRIVE THANH CONG!")
    print(f"  FILE: {filename}")
    print(f"  THU MUC: https://drive.google.com/drive/folders/{TARGET_FOLDER_ID}")
    print(f"  LINK TRUC TIEP TEP: {view_link}")
    print("=========================================================================")
    return view_link

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else r"D:\Tool Revit\release\BIN-Tool-CurrentUser-NoAdmin-20260903.zip"
    upload_file(target)
