﻿using AlephNote.PluginInterface;
using AlephNote.PluginInterface.Impl;
using AlephNote.PluginInterface.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AlephNote.Plugins.StandardNote
{
	public class StandardNotePlugin : BasicRemotePlugin
	{
		public static readonly Version Version = GetInformationalVersion(typeof(StandardNotePlugin).GetTypeInfo().Assembly);
		public const string Name = "StandardNotePlugin";

		public const string CURRENT_SCHEMA = "002";

		private IAlephLogger _logger;

		public StandardNotePlugin() : base("Standard Notes", Version, Guid.Parse("30d867a4-cbdc-45c5-950a-c119bf2f2845"))
		{
			//
		}

		public override void Init(IAlephLogger logger)
		{
			_logger = logger;
		}

		public override IRemoteStorageConfiguration CreateEmptyRemoteStorageConfiguration()
		{
			return new StandardNoteConfig();
		}

		public override IRemoteStorageConnection CreateRemoteStorageConnection(IWebProxy proxy, IRemoteStorageConfiguration config, HierachyEmulationConfig hConfig)
		{
			return new StandardNoteConnection(_logger, proxy, (StandardNoteConfig)config, hConfig);
		}

		public override INote CreateEmptyNote(IRemoteStorageConnection iconn, IRemoteStorageConfiguration cfg)
		{
			var conn = (StandardNoteConnection)iconn;
			return new StandardFileNote(Guid.NewGuid(), (StandardNoteConfig) cfg, conn.HConfig) {ContentVersion = CURRENT_SCHEMA};
		}

		public override IRemoteStorageSyncPersistance CreateEmptyRemoteSyncData()
		{
			return new StandardNoteData();
		}

		public override IEnumerable<Tuple<string, string>> CreateHelpTexts()
		{
			yield return Tuple.Create("SendEncrypted", "If checked the note content is encrypted locally before being send to the server."+"\n"+
				                                       "No one can read your notes without your password, not even the server administrator.");
			yield return Tuple.Create("RemEmptyTags", "If checked tags that are not linked with any note are deleted on the server.");
		}

		public override bool HasNativeDirectorySupport()
		{
			return false;
		}
	}
}
