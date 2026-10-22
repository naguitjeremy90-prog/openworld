using System;
using System.Collections.Generic;

namespace Alaala.Saving
{
    public static class AlaalaSaveSchema
    {
        public const int Version = 1;
        public const string ContentVersion = "alaala-foundation-1";
        public const int MaximumTextLength = 32768;
    }

    public enum SaveRunKind { Unverified, Genuine, Synthetic }
    public enum SaveStateClass { Story, PlayerAuthored, CompletedPresentation, Temporary, Development }
    public enum SaveValueKind { Boolean, Integer, String }

    // Lists and concrete records deliberately support JsonUtility. No Unity object references.
    [Serializable] public sealed class SaveBoolean { public string key; public bool value; }
    [Serializable] public sealed class SaveInteger { public string key; public int value; }
    [Serializable] public sealed class SaveString { public string key; public string value; }

    [Serializable]
    public sealed class AlaalaSessionSnapshot
    {
        public List<SaveBoolean> booleans = new List<SaveBoolean>();
        public List<SaveInteger> integers = new List<SaveInteger>();
        public List<SaveString> strings = new List<SaveString>();
    }

    [Serializable]
    public sealed class AlaalaTaskSnapshot
    {
        public string taskId;
        public TaskState state;
        public string stageId = "";
        public string objective = "";
    }

    [Serializable]
    public sealed class AlaalaInventorySnapshot
    {
        public List<string> owned = new List<string>();
        public List<string> collected = new List<string>();
        public List<string> consumed = new List<string>();
    }

    [Serializable]
    public sealed class AlaalaWorldSnapshot
    {
        public bool isMorning;
        public bool canTeleport;
        public bool movementDone;
        public bool playerMovementDone;
    }

    [Serializable]
    public sealed class AlaalaSaveSnapshot
    {
        public int schemaVersion = AlaalaSaveSchema.Version;
        public string contentVersion = AlaalaSaveSchema.ContentVersion;
        public string gameVersion;
        public long saveRevision;
        public string titleId;
        public string playFabAccountId;
        public SaveRunKind runKind;
        public string runId;
        public string checkpointId;
        public string targetScene;
        public string spawnRoute;
        public string activeMainTaskId = "";
        // Journal discoveries/stages/answers, analysis results, unlocks and completed
        // teaching history use the explicit session-key registry, not copied descriptions.
        public AlaalaSessionSnapshot session = new AlaalaSessionSnapshot();
        public List<AlaalaTaskSnapshot> tasks = new List<AlaalaTaskSnapshot>();
        public AlaalaInventorySnapshot inventory = new AlaalaInventorySnapshot();
        public AlaalaWorldSnapshot world = new AlaalaWorldSnapshot();
    }

    public readonly struct AlaalaSaveOwner
    {
        public readonly string TitleId, AccountId;
        internal AlaalaSaveOwner(string title, string account) { TitleId = title; AccountId = account; }
        public static bool TryGetAuthenticated(out AlaalaSaveOwner owner)
        {
            owner = default;
            var session = Authentication.AuthenticationServices.Current.Session;
            if (session == null || session.Provider != "PlayFab" || string.IsNullOrWhiteSpace(session.AccountId) ||
                string.IsNullOrWhiteSpace(PlayFab.PlayFabSettings.TitleId)) return false;
            owner = new AlaalaSaveOwner(PlayFab.PlayFabSettings.TitleId, session.AccountId);
            return true;
        }
    }
}
