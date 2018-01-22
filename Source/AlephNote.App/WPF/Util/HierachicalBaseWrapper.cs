﻿using AlephNote.Common.MVVM;
using AlephNote.PluginInterface;
using AlephNote.PluginInterface.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
namespace AlephNote.WPF.Util
{
	public interface IHierachicalWrapperConfig
	{
		bool SearchFilter(INote note);
		IComparer<INote> DisplaySorter();
		bool ShowAllNotesNode { get; }
		bool ShowEmptyPathNode { get; }
	}

	public abstract class HierachicalBaseWrapper : ObservableObject
	{
		public abstract string Header { get; }

		private bool _isSelected = false;
		public bool IsSelected { get { return _isSelected; } set { _isSelected = value; OnPropertyChanged(); } }

		private bool _isExpanded = true;
		public bool IsExpanded { get { return _isExpanded; } set { _isExpanded = value; OnPropertyChanged(); } }

		public abstract string IconSource { get; }
		public abstract int Order { get; }
		public abstract bool IsSpecialNode { get; }

		public abstract void Sync(HierachicalBaseWrapper aother, HierachicalFolderWrapper[] parents);
	}

	public class HierachicalFlatViewWrapper : HierachicalFolderWrapper
	{
		private readonly HierachicalFolderWrapper _baseWrapper;

		public override string IconSource => "/Resources/folder_all.png";
		public override int Order => -2;
		public override bool IsSpecialNode => true;

		public override void Sync(HierachicalBaseWrapper aother, HierachicalFolderWrapper[] parents)
		{
			var other = (HierachicalFlatViewWrapper)aother;

			AllSubNotes.SynchronizeCollectionSafe(other.AllSubNotes);
		}

		public override IEnumerable<INote> GetAllSubNotes(bool rec) => _baseWrapper.GetAllSubNotes(true);

		public HierachicalFlatViewWrapper(HierachicalFolderWrapper baseWrapper, IHierachicalWrapperConfig cfg) : base("All notes", cfg, DirectoryPath.Root(), false, false)
		{
			_baseWrapper = baseWrapper;
			IsSelected = true;
		}
	}

	public class HierachicalEmptyPathViewWrapper : HierachicalFolderWrapper
	{
		private readonly HierachicalFolderWrapper _baseWrapper;

		public override string IconSource => "/Resources/folder_none.png";
		public override int Order => -1;
		public override bool IsSpecialNode => true;

		public override void Sync(HierachicalBaseWrapper aother, HierachicalFolderWrapper[] parents)
		{
			var other = (HierachicalEmptyPathViewWrapper)aother;

			AllSubNotes.SynchronizeCollectionSafe(other.AllSubNotes);
		}

		public override IEnumerable<INote> GetAllSubNotes(bool rec) => _baseWrapper.GetAllSubNotes(true).Where(n => n.Path.IsRoot());

		public HierachicalEmptyPathViewWrapper(HierachicalFolderWrapper baseWrapper, IHierachicalWrapperConfig cfg) : base("Unsorted notes", cfg, DirectoryPath.Root(), false, false)
		{
			_baseWrapper = baseWrapper;
		}
	}

	public class HierachicalFolderWrapper : HierachicalBaseWrapper
	{
		private readonly bool _isRoot;
		private readonly IHierachicalWrapperConfig _config;
		private string _header;
		public override string Header => _header;

		private DirectoryPath _path;

		public override string IconSource => _path.IsRoot() ? "/Resources/folder_root.png" : "/Resources/folder_any.png";
		public override int Order => 0;
		public override bool IsSpecialNode => false;

		public HierachicalFlatViewWrapper AllNotesViewWrapper;
		public HierachicalEmptyPathViewWrapper EmptyPathViewWrapper;

		private ObservableCollection<HierachicalFolderWrapper> _subFolders = new ObservableCollection<HierachicalFolderWrapper>();
		public ObservableCollection<HierachicalFolderWrapper> SubFolder { get { return _subFolders; } }

		public ObservableCollection<INote> AllSubNotes { get; set; } = new ObservableCollection<INote>();
		private List<INote> _directSubNotes = new List<INote>();

		public virtual IEnumerable<INote> GetAllSubNotes(bool rec)
		{
			if (!rec)
				return _directSubNotes
						.Where(_config.SearchFilter)
						.OrderBy(p => p, _config.DisplaySorter());
			else
				return _directSubNotes
						.Concat(SubFolder.Where(sf => !sf.IsSpecialNode)
						.SelectMany(sf => sf.GetAllSubNotes(true)))
						.Where(_config.SearchFilter)
						.OrderBy(p => p, _config.DisplaySorter());
		}

		public bool Permanent = false;

		public HierachicalFolderWrapper(string header, IHierachicalWrapperConfig cfg, DirectoryPath path, bool root, bool perm)
		{
			_isRoot = root;
			_path = path;
			_header = header;
			_config = cfg;
			Permanent = perm;

			if (_isRoot && cfg.ShowAllNotesNode) SubFolder.Add(AllNotesViewWrapper  = new HierachicalFlatViewWrapper(this, cfg));
			if (_isRoot && cfg.ShowEmptyPathNode) SubFolder.Add(EmptyPathViewWrapper = new HierachicalEmptyPathViewWrapper(this, cfg));
		}

		public HierachicalFolderWrapper GetOrCreateFolder(string txt, out bool created)
		{
			foreach (var item in SubFolder)
			{
				if (item.Header.ToLower() == txt.ToLower()) { created = false; return item;}
			}
			var i = new HierachicalFolderWrapper(txt, _config, _path.SubDir(txt), false, false);
			SubFolder.Add(i);
			created = true;
			return i;
		}

		public HierachicalFolderWrapper CreateRootFolder()
		{
			var i = new HierachicalFolderWrapper("[My Notes]", _config, DirectoryPath.Root(), false, false);
			SubFolder.Add(i);
			return i;
		}

		public void TriggerAllSubNotesChanged()
		{
			OnPropertyChanged("DirectSubNotes");
			OnPropertyChanged("AllSubNotes");
		}

		public override void Sync(HierachicalBaseWrapper aother, HierachicalFolderWrapper[] parents)
		{
			var other = (HierachicalFolderWrapper)aother;

			Debug.Assert(this.Header.ToLower() == other.Header.ToLower());
			Debug.Assert(this._isRoot == other._isRoot);

			_path = other._path;
			_directSubNotes = other._directSubNotes;
			AllSubNotes.SynchronizeCollectionSafe(other.AllSubNotes);

			var me = this;

			bool FCompare(HierachicalFolderWrapper a, HierachicalFolderWrapper b) => a.Header.ToLower() == b.Header.ToLower();
			HierachicalFolderWrapper FCopy(HierachicalFolderWrapper a)
			{
				if (a.GetType() == typeof(HierachicalFolderWrapper))        return new HierachicalFolderWrapper(a.Header, _config, a._path, a._isRoot, a.Permanent);
				if (a.GetType() == typeof(HierachicalEmptyPathViewWrapper)) return me.EmptyPathViewWrapper = new HierachicalEmptyPathViewWrapper(me, _config);
				if (a.GetType() == typeof(HierachicalFlatViewWrapper))      return me.AllNotesViewWrapper  = new HierachicalFlatViewWrapper(me, _config);

				throw new NotSupportedException(a.GetType().ToString());
			}
			SubFolder.SynchronizeCollection(other.SubFolder, FCompare, FCopy);
			Debug.Assert(SubFolder.Count == other.SubFolder.Count);

			for (int i = 0; i < SubFolder.Count; i++) SubFolder[i].Sync(other.SubFolder[i], parents.Concat(new []{this}).ToArray());
		}

		public void CopyPermanentsTo(HierachicalFolderWrapper other)
		{
			foreach (var folder1 in SubFolder)
			{
				var folder2 = other.SubFolder.FirstOrDefault(sf => sf._path.EqualsIgnoreCase(folder1._path));
				if (folder2 == null && folder1.Permanent)
				{
					folder2 = other.GetOrCreateFolder(folder1.Header, out _);
					folder2.Permanent = true;
				}

				if (folder2 != null) folder1.CopyPermanentsTo(folder2);
			}
		}

		public void ClearPermanents()
		{
			Permanent = false;
			foreach (var sf in SubFolder) sf.ClearPermanents();
		}

		public void Add(HierachicalFolderWrapper elem) { SubFolder.Add(elem); }
		public void Add(INote elem) { _directSubNotes.Add(elem); }

		public void Clear()
		{
			SubFolder.Clear();
			if (_isRoot) SubFolder.Add(new HierachicalFlatViewWrapper(this, _config));

			_directSubNotes.Clear();

			AllSubNotes.Clear();
		}

		public override string ToString()
		{
			return $"{{{Header}}}::[{string.Join(", ", _directSubNotes)}]";
		}

		public HierachicalFolderWrapper Find(INote note)
		{
			foreach (var item in SubFolder)
			{
				if (item.IsSpecialNode) continue;
				var n = item.Find(note);
				if (n != null) return null;
			}
			foreach (var item in _directSubNotes)
			{
				if (item == note) return this;
			}
			return null;
		}

		public HierachicalFolderWrapper Find(DirectoryPath path)
		{
			foreach (var item in SubFolder)
			{
				if (item.IsSpecialNode) continue;
				if (item._path.EqualsIgnoreCase(path)) return item;
				var n = item.Find(path);
				if (n != null) return n;
			}
			return null;
		}

		public DirectoryPath GetNewNotePath()
		{
			return _path;
		}

		public bool RemoveFind(DirectoryPath folder)
		{
			for (int i = 0; i < SubFolder.Count; i++)
			{
				if (SubFolder[i].GetNewNotePath().EqualsIgnoreCase(folder)) { SubFolder.RemoveAt(i); return true; }
				if (SubFolder[i].RemoveFind(folder)) return true;
			}
			return false;
		}

		public void Sort()
		{
			SubFolder.SynchronizeCollectionSafe(SubFolder.OrderBy(p => p.Order).ThenBy(p => p.Header.ToLower()));
			foreach (var sf in SubFolder) sf.Sort();
		}

		public void FinalizeCollection(bool rec)
		{
			AllSubNotes.Synchronize(GetAllSubNotes(rec).ToList());
			foreach (var sf in SubFolder) sf.FinalizeCollection(rec);
		}

		public IEnumerable<DirectoryPath> ListPaths()
		{
			yield return _path;
			foreach (var sf in SubFolder)
			{
				foreach (var p in sf.ListPaths()) yield return p;
			}
		}
	}
}
