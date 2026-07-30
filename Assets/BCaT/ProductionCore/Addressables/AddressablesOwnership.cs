using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BCaT.Production.Addressing
{
    /// <summary>
    /// The Addressables ownership model: every load has a named owner, a stored
    /// handle, and documented load/release points. Loaders notify the registry
    /// when handles are created and released; the registry detects duplicate
    /// loads, releases without ownership, and leaked handles, and exposes the
    /// active count for the repeat-entry validation used by the smoke test.
    /// Wraps (does not replace) the existing AddressableSceneHandleStore.
    /// </summary>
    public static class AddressablesHandleRegistry
    {
        public sealed class Record
        {
            public string Owner;
            public string Key;
            public AsyncOperationHandle Handle;
            public DateTime CreatedAt;
        }

        static readonly Dictionary<string, Record> records = new Dictionary<string, Record>();

        public static int ActiveCount => records.Count;

        static string Id(string owner, string key) => owner + "::" + key;

        public static void NotifyCreated(string owner, string key, AsyncOperationHandle handle)
        {
            string id = Id(owner, key);
            if (records.ContainsKey(id))
                AddressablesLifecycleLog.Warn($"DUPLICATE LOAD: '{key}' already held by '{owner}'.");

            records[id] = new Record
            {
                Owner = owner,
                Key = key,
                Handle = handle,
                CreatedAt = DateTime.Now,
            };
            AddressablesLifecycleLog.Log($"handle created: owner='{owner}' key='{key}' active={ActiveCount}");
        }

        public static void NotifyCompleted(string owner, string key, AsyncOperationStatus status)
        {
            AddressablesLifecycleLog.Log($"handle completed: owner='{owner}' key='{key}' status={status}");
            if (status == AsyncOperationStatus.Failed)
                AddressablesLifecycleLog.Warn($"LOAD FAILED: owner='{owner}' key='{key}'.");
        }

        public static void NotifyReleased(string owner, string key)
        {
            string id = Id(owner, key);
            if (!records.Remove(id))
                AddressablesLifecycleLog.Warn($"RELEASE WITHOUT OWNERSHIP: owner='{owner}' key='{key}'.");
            else
                AddressablesLifecycleLog.Log($"handle released: owner='{owner}' key='{key}' active={ActiveCount}");
        }

        /// <summary>Diagnostic dump for smoke tests and support logs.</summary>
        public static string Dump()
        {
            if (records.Count == 0) return "AddressablesHandleRegistry: no active handles.";
            var sb = new StringBuilder("AddressablesHandleRegistry active handles:\n");
            foreach (var r in records.Values)
                sb.AppendLine($"  owner='{r.Owner}' key='{r.Key}' since={r.CreatedAt:HH:mm:ss} valid={r.Handle.IsValid()}");
            return sb.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => records.Clear();
    }

    /// <summary>
    /// Addressables lifecycle logging. Verbose in development builds by
    /// default; enable in release builds with -bcatAddressablesLog. Warnings
    /// (duplicates, failures, unowned releases) always log.
    /// </summary>
    public static class AddressablesLifecycleLog
    {
        static bool? verbose;

        public static bool Verbose
        {
            get
            {
                if (!verbose.HasValue)
                {
                    verbose = Debug.isDebugBuild;
                    foreach (var arg in Environment.GetCommandLineArgs())
                        if (string.Equals(arg, "-bcatAddressablesLog", StringComparison.OrdinalIgnoreCase))
                            verbose = true;
                }
                return verbose.Value;
            }
        }

        public static void Log(string message)
        {
            if (Verbose)
                Debug.Log("[Addressables] " + message);
        }

        public static void Warn(string message) => Debug.LogWarning("[Addressables] " + message);
    }
}
