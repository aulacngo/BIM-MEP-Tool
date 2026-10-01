using System;
using System.Collections.Generic;
using System.Threading;

// Offline API surface only. Real add-in builds still reference Autodesk DLLs.
namespace Autodesk.Revit.DB
{
	internal static class ApiThread
	{
		internal static readonly int Main = Thread.CurrentThread.ManagedThreadId;
		internal static int Violations;
		internal static void Check()
		{
			if (Thread.CurrentThread.ManagedThreadId == Main) return;
			Interlocked.Increment(ref Violations);
			throw new InvalidOperationException("Revit API used off the main thread.");
		}
	}
	public sealed class ElementId
	{
		internal readonly int Value;
		internal ElementId(int value) { Value = value; }
	}
	public sealed class Category
	{
		internal string CategoryName = "Pipes";
		public string Name { get { ApiThread.Check(); return CategoryName; } }
	}
	public class Element
	{
		internal Category ElementCategory = new Category();
		public Category Category { get { ApiThread.Check(); return ElementCategory; } }
	}
	public sealed class View : Element
	{
		internal string ViewName = "Level 1";
		public string Name { get { ApiThread.Check(); return ViewName; } }
	}
	public sealed class Application
	{
		internal string User = "Telemetry test";
		public string Username { get { ApiThread.Check(); return User; } }
	}
	public sealed class Document
	{
		internal bool Family;
		internal bool ThrowView;
		internal bool ThrowElement;
		internal string DocumentTitle = "Test project";
		internal View CurrentView = new View();
		internal Application App = new Application();
		internal readonly Dictionary<int, Element> Elements = new Dictionary<int, Element>();
		internal int Lookups;
		public bool IsValidObject { get { ApiThread.Check(); return true; } }
		public bool IsFamilyDocument { get { ApiThread.Check(); return Family; } }
		public string Title { get { ApiThread.Check(); return DocumentTitle; } }
		public Application Application { get { ApiThread.Check(); return App; } }
		public View ActiveView
		{
			get { ApiThread.Check(); if (ThrowView) throw new InvalidOperationException(); return CurrentView; }
		}
		public Element GetElement(ElementId id)
		{
			ApiThread.Check();
			Lookups++;
			if (ThrowElement) throw new InvalidOperationException();
			return Elements.TryGetValue(id.Value, out Element element) ? element : null;
		}
	}
}

namespace Autodesk.Revit.DB.Events
{
	public enum UndoOperation { TransactionCommitted, TransactionUndone, TransactionRedone }
	public sealed class DocumentChangedEventArgs : EventArgs
	{
		internal Document Document;
		internal List<ElementId> Added = new List<ElementId>();
		internal List<ElementId> Deleted = new List<ElementId>();
		internal List<ElementId> Modified = new List<ElementId>();
		internal List<string> Names = new List<string> { "Draw pipes" };
		internal UndoOperation Kind;
		public UndoOperation Operation { get { ApiThread.Check(); return Kind; } }
		public Document GetDocument() { ApiThread.Check(); return Document; }
		public ICollection<ElementId> GetAddedElementIds() { ApiThread.Check(); return Added; }
		public ICollection<ElementId> GetDeletedElementIds() { ApiThread.Check(); return Deleted; }
		public ICollection<ElementId> GetModifiedElementIds() { ApiThread.Check(); return Modified; }
		public ICollection<string> GetTransactionNames() { ApiThread.Check(); return Names; }
	}
}
