using System.Security.Cryptography;
using System.Text.Json;

namespace JmcModLib.Security.Backends;

internal sealed class WeakFileSecretBackend : ISecretBackend
{
    public string BackendId => "weak-file";

    public JmcSecretProtectionLevel ProtectionLevel => JmcSecretProtectionLevel.WeakFileProtection;

    public bool IsAvailable => true;

    public bool TryRead(SecretIdentifier id, out byte[] value, out JmcSecretReadStatus status)
    {
        value = [];
        status = JmcSecretReadStatus.Missing;

        if (!File.Exists(id.WeakFilePath))
        {
            return false;
        }

        SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
        if (document == null)
        {
            status = JmcSecretReadStatus.BackendError;
            return false;
        }

        if (!document.Items.TryGetValue(id.StorageEntryKey, out string? encoded)
            || string.IsNullOrWhiteSpace(encoded))
        {
            status = JmcSecretReadStatus.Missing;
            return false;
        }

        try
        {
            value = Convert.FromBase64String(encoded);
            status = JmcSecretReadStatus.Success;
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"弱保护 Secret 文件条目损坏：{id.Key}", ex, id.Assembly);
            status = JmcSecretReadStatus.BackendError;
            return false;
        }
    }

    public bool TrySave(SecretIdentifier id, ReadOnlySpan<byte> value, out JmcSecretWriteStatus status)
    {
        try
        {
            Directory.CreateDirectory(id.ModSecretDirectory);
            SecretFileDocument document = ReadDocument(id, tolerateFailure: true) ?? new SecretFileDocument();
            document.Items[id.StorageEntryKey] = Convert.ToBase64String(value);
            WriteDocument(id.WeakFilePath, document);
            TryHardenPermissions(id.WeakFilePath, id.Assembly);
            ModLogger.Warn($"Secret {id.Key} 已使用弱保护文件保存；这不是安全加密。", id.Assembly);
            status = JmcSecretWriteStatus.Success;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            ModLogger.Warn($"写入弱保护 Secret 文件被拒绝：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.AccessDenied;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"写入弱保护 Secret 文件失败：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.BackendError;
            return false;
        }
    }

    public bool TryDelete(SecretIdentifier id, out JmcSecretWriteStatus status)
    {
        status = JmcSecretWriteStatus.Success;
        if (!File.Exists(id.WeakFilePath))
        {
            return true;
        }

        try
        {
            SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
            if (document == null)
            {
                File.Delete(id.WeakFilePath);
                return true;
            }

            bool removed = document.Items.Remove(id.StorageEntryKey);
            if (document.Items.Count == 0)
            {
                File.Delete(id.WeakFilePath);
            }
            else if (removed)
            {
                WriteDocument(id.WeakFilePath, document);
                TryHardenPermissions(id.WeakFilePath, id.Assembly);
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            ModLogger.Warn($"删除弱保护 Secret 被拒绝：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.AccessDenied;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"删除弱保护 Secret 失败：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.BackendError;
            return false;
        }
    }

    public bool Exists(SecretIdentifier id)
    {
        SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
        return document?.Items.ContainsKey(id.StorageEntryKey) == true;
    }

    private static SecretFileDocument? ReadDocument(SecretIdentifier id, bool tolerateFailure)
    {
        try
        {
            if (!File.Exists(id.WeakFilePath))
            {
                return new SecretFileDocument();
            }

            string json = File.ReadAllText(id.WeakFilePath);
            return JsonSerializer.Deserialize<SecretFileDocument>(json) ?? new SecretFileDocument();
        }
        catch (Exception ex) when (tolerateFailure)
        {
            ModLogger.Warn($"读取弱保护 Secret 文件失败：{id.WeakFilePath}", ex, id.Assembly);
            return null;
        }
    }

    private static void WriteDocument(string path, SecretFileDocument document)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(document, options));
    }

    private static void TryHardenPermissions(string path, System.Reflection.Assembly assembly)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"无法收紧弱保护 Secret 文件权限：{path}", ex, assembly);
        }
    }
}
