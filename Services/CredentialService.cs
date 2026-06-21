using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CredentialManagement;

// Verwaltet den Zabbix-User-Token im Windows-CredentialManager

namespace ZabbixTrayMonitor.Services
{
    public static class CredentialService
    {
        public const string DefaultCredentialUsername = "api-token";
        private const string DefaultAppName = "ZabbixTrayMonitor";
        private const string DefaultCredentialTargetSuffix = "ApiToken";

        private const uint CredentialTypeGeneric = 1;

        // Windows API um Credentials zu filtern um alte Targets mit gleichem App-Prefix entfernen zu können weil das NuGet Paket CredentialManagement das nicht kann
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredEnumerate(
            string filter,
            int flags,
            out int count,
            out IntPtr credentials);

        // Gibt den Speicher wieder frei den CredEnumerate zurückgibt
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree(IntPtr buffer);

        // Native Windows Credential Struktur wird  gebraucht um TargetName und Type aus CredEnumerate lesen zu können
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        // Baut Credential-Target Namen aus AppName + Suffix
        public static string BuildTarget(string appName, string suffix)
        {
            var safeAppName = string.IsNullOrWhiteSpace(appName)
                ? DefaultAppName
                : appName.Trim();

            var safeSuffix = string.IsNullOrWhiteSpace(suffix)
                ? DefaultCredentialTargetSuffix
                : suffix.Trim();

            return $"{safeAppName}.{safeSuffix}";
        }

        public static void SaveToken(string token, string target, string username)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Credential token darf nicht leer sein", nameof(token));

            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("Credential target darf nicht leer sein", nameof(target));

            if (string.IsNullOrWhiteSpace(username))
                username = DefaultCredentialUsername;

            using var credential = new Credential
            {
                Target = target.Trim(),
                Username = username.Trim(),
                Password = token,
                Type = CredentialType.Generic,
                PersistanceType = PersistanceType.LocalComputer
            };

            credential.Save();
        }

        public static string? GetToken(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return null;

            using var credential = new Credential
            {
                Target = target.Trim(),
                Type = CredentialType.Generic
            };

            return credential.Load() ? credential.Password : null;
        }

        public static bool HasToken(string target)
        {
            return !string.IsNullOrWhiteSpace(GetToken(target));
        }

        public static void DeleteToken(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return;

            using var credential = new Credential
            {
                Target = target.Trim(),
                Type = CredentialType.Generic
            };

            credential.Delete();
        }

        // Liest Benutzernamen für Target
        public static string? GetUsername(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return null;

            using var credential = new Credential
            {
                Target = target.Trim(),
                Type = CredentialType.Generic
            };

            return credential.Load() ? credential.Username : null;
        }

        public static bool SaveOrUpdateCredential(
            string? oldTarget,
            string newTarget,
            string username,
            string? newToken)
        {
            if (string.IsNullOrWhiteSpace(newTarget))
                throw new ArgumentException("Credential target darf nicht leer sein", nameof(newTarget));

            if (string.IsNullOrWhiteSpace(username))
                username = DefaultCredentialUsername;

            var normalizedOldTarget = oldTarget?.Trim();
            var normalizedNewTarget = newTarget.Trim();

            // wenn neuer Token den nehmen sonst vorhandenen Token vom alten oder neuen Target übernehmen
            var tokenToSave = !string.IsNullOrWhiteSpace(newToken)
                ? newToken.Trim()
                : GetExistingTokenForUpdate(normalizedOldTarget, normalizedNewTarget);

            if (string.IsNullOrWhiteSpace(tokenToSave))
                return false;

            SaveToken(tokenToSave, normalizedNewTarget, username);

            // wenn Target geändert altes Target entfernen
            if (!string.IsNullOrWhiteSpace(normalizedOldTarget) &&
                !string.Equals(normalizedOldTarget, normalizedNewTarget, StringComparison.OrdinalIgnoreCase))
            {
                DeleteToken(normalizedOldTarget);
            }

            // löscht weitere alte Targets mit gleichem App-Prefix wenn config.json gelöscht wurde und alte Credentials noch im Windows Credential Manager liegen
            DeleteOtherTokensWithSameAppPrefix(normalizedNewTarget);

            return true;
        }

        // Sucht einen vorhandenen Token den wir beim Aktualisieren weiterverwenden können
        // zuerst altes Target, danach neues Target
        private static string? GetExistingTokenForUpdate(string? oldTarget, string newTarget)
        {
            if (!string.IsNullOrWhiteSpace(oldTarget))
            {
                var oldToken = GetToken(oldTarget);

                if (!string.IsNullOrWhiteSpace(oldToken))
                    return oldToken;
            }

            var currentToken = GetToken(newTarget);

            if (!string.IsNullOrWhiteSpace(currentToken))
                return currentToken;

            return null;
        }

        // Löscht alle anderen Generic Credentials mit gleichem App-Prefix
        private static void DeleteOtherTokensWithSameAppPrefix(string keepTarget)
        {
            try
            {
                var prefix = GetCredentialTargetPrefix(keepTarget);

                if (string.IsNullOrWhiteSpace(prefix))
                    return;

                var targets = EnumerateGenericCredentialTargets(prefix + "*");

                foreach (var target in targets)
                {
                    if (string.Equals(target, keepTarget, StringComparison.OrdinalIgnoreCase))
                        continue;

                    DeleteToken(target);
                }
            }
            catch { }
        }

        // holt Prefix eines Targets zB. ZabbixTrayMon.api -> ZabbixTrayMon.
        private static string? GetCredentialTargetPrefix(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return null;

            var normalizedTarget = target.Trim();
            var lastDotIndex = normalizedTarget.LastIndexOf('.');

            if (lastDotIndex <= 0)
                return null;

            return normalizedTarget.Substring(0, lastDotIndex + 1);
        }

        // Listet alle Credentials auf die zum Filter passen zB.: "ZabbixTrayMon.*" ... WindowsAPI gibt keine Liste zurück sondern Pointer auf die Credentials https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credenumeratew
        private static List<string> EnumerateGenericCredentialTargets(string filter)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(filter))
                return result;

            if (!CredEnumerate(filter, 0, out var count, out var credentialsPointer))
                return result;

            try
            {
                for (var i = 0; i < count; i++)
                {
                    var credentialPointer = Marshal.ReadIntPtr(credentialsPointer, i * IntPtr.Size);
                    var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);

                    if (credential.Type != CredentialTypeGeneric)
                        continue;

                    if (!string.IsNullOrWhiteSpace(credential.TargetName))
                        result.Add(credential.TargetName);
                }
            }
            finally
            {
                CredFree(credentialsPointer);
            }

            return result;
        }

        public static string? GetTokenForApp(string appName, string suffix)
        {
            try
            {
                var target = BuildTarget(appName, suffix);
                return GetToken(target);
            }
            catch
            {
                return null;
            }
        }
    }
}