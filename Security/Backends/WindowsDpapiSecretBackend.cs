using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace JmcModLib.Security.Backends;

internal sealed class WindowsDpapiSecretBackend : ISecretBackend
{
    private const string SecretDescription = "JmcModLib SecretStore";
    private const int CurrentUserFlags = 0;

    public string BackendId => "windows-dpapi-current-user";

    public JmcSecretProtectionLevel ProtectionLevel => JmcSecretProtectionLevel.UserProfileProtected;

    public bool IsAvailable => OperatingSystem.IsWindows();

    public bool TryRead(SecretIdentifier id, out byte[] value, out JmcSecretReadStatus status)
    {
        value = [];
        status = JmcSecretReadStatus.Missing;

        if (!IsAvailable)
        {
            status = JmcSecretReadStatus.Unavailable;
            return false;
        }

        if (!File.Exists(id.SecureFilePath))
        {
            return false;
        }

        SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
        if (document == null)
        {
            status = JmcSecretReadStatus.BackendError;
            return false;
        }

        if (!document.Items.TryGetValue(id.StorageEntryKey, out string? encryptedText)
            || string.IsNullOrWhiteSpace(encryptedText))
        {
            status = JmcSecretReadStatus.Missing;
            return false;
        }

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(encryptedText);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Secret 密文条目格式损坏，需要重新设置：{id.Key}", ex, id.Assembly);
            status = JmcSecretReadStatus.DecryptionFailed;
            return false;
        }

        try
        {
            value = ProtectOrUnprotect(encrypted, protect: false);
            status = JmcSecretReadStatus.Success;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            ModLogger.Warn($"读取 Secret 被拒绝：{id.Key}", ex, id.Assembly);
            status = JmcSecretReadStatus.AccessDenied;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Secret 解密失败，需要重新设置：{id.Key}", ex, id.Assembly);
            status = JmcSecretReadStatus.DecryptionFailed;
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public bool TrySave(SecretIdentifier id, ReadOnlySpan<byte> value, out JmcSecretWriteStatus status)
    {
        if (!IsAvailable)
        {
            status = JmcSecretWriteStatus.Unavailable;
            return false;
        }

        byte[] plain = value.ToArray();
        byte[] encrypted = [];
        try
        {
            Directory.CreateDirectory(id.ModSecretDirectory);
            SecretFileDocument document = ReadDocument(id, tolerateFailure: true) ?? new SecretFileDocument();
            encrypted = ProtectOrUnprotect(plain, protect: true);
            document.Items[id.StorageEntryKey] = Convert.ToBase64String(encrypted);
            WriteDocument(id.SecureFilePath, document);
            status = JmcSecretWriteStatus.Success;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            ModLogger.Warn($"写入 Secret 被拒绝：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.AccessDenied;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"写入 Secret 失败：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.BackendError;
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public bool TryDelete(SecretIdentifier id, out JmcSecretWriteStatus status)
    {
        status = JmcSecretWriteStatus.Success;

        if (!IsAvailable)
        {
            status = JmcSecretWriteStatus.Unavailable;
            return false;
        }

        if (!File.Exists(id.SecureFilePath))
        {
            return true;
        }

        try
        {
            SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
            if (document == null)
            {
                File.Delete(id.SecureFilePath);
                return true;
            }

            bool removed = document.Items.Remove(id.StorageEntryKey);
            if (document.Items.Count == 0)
            {
                File.Delete(id.SecureFilePath);
            }
            else if (removed)
            {
                WriteDocument(id.SecureFilePath, document);
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            ModLogger.Warn($"删除 Secret 被拒绝：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.AccessDenied;
            return false;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"删除 Secret 失败：{id.Key}", ex, id.Assembly);
            status = JmcSecretWriteStatus.BackendError;
            return false;
        }
    }

    public bool Exists(SecretIdentifier id)
    {
        if (!IsAvailable)
        {
            return false;
        }

        SecretFileDocument? document = ReadDocument(id, tolerateFailure: true);
        return document?.Items.ContainsKey(id.StorageEntryKey) == true;
    }

    private static SecretFileDocument? ReadDocument(SecretIdentifier id, bool tolerateFailure)
    {
        try
        {
            if (!File.Exists(id.SecureFilePath))
            {
                return new SecretFileDocument();
            }

            string json = File.ReadAllText(id.SecureFilePath);
            return JsonSerializer.Deserialize<SecretFileDocument>(json) ?? new SecretFileDocument();
        }
        catch (Exception ex) when (tolerateFailure)
        {
            ModLogger.Warn($"读取 Secret 文件失败：{id.SecureFilePath}", ex, id.Assembly);
            return null;
        }
    }

    private static void WriteDocument(string path, SecretFileDocument document)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(document, options));
    }

    private static byte[] ProtectOrUnprotect(byte[] input, bool protect)
    {
        DataBlob inBlob = DataBlob.FromBytes(input);
        DataBlob outBlob = default;
        try
        {
            bool ok = protect
                ? CryptProtectData(ref inBlob, SecretDescription, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CurrentUserFlags, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CurrentUserFlags, ref outBlob);

            if (!ok)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Windows DPAPI 调用失败，错误码：{errorCode}");
            }

            return outBlob.ToArray();
        }
        finally
        {
            inBlob.Free();
            outBlob.FreeWithLocalFree();
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;

        public static DataBlob FromBytes(byte[] bytes)
        {
            IntPtr data = Marshal.AllocHGlobal(bytes.Length);
            if (bytes.Length > 0)
            {
                Marshal.Copy(bytes, 0, data, bytes.Length);
            }

            return new DataBlob
            {
                Size = bytes.Length,
                Data = data
            };
        }

        public byte[] ToArray()
        {
            if (Size <= 0 || Data == IntPtr.Zero)
            {
                return [];
            }

            byte[] bytes = new byte[Size];
            Marshal.Copy(Data, bytes, 0, Size);
            return bytes;
        }

        public void Free()
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(Data);
            Data = IntPtr.Zero;
            Size = 0;
        }

        public void FreeWithLocalFree()
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            _ = LocalFree(Data);
            Data = IntPtr.Zero;
            Size = 0;
        }
    }
}
