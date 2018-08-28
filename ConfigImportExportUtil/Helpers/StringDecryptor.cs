using System.IO;
using System.Reflection;

namespace ConfigImportExportUtil.Helpers
{
    internal class StringDecryptor
    {
        private readonly string _installPath;

        public StringDecryptor(string professionalInstallPath = null)
        {
            if (string.IsNullOrEmpty(professionalInstallPath))
            {
                _installPath = @"C:\Program Files (x86)\Milestone\Milestone Surveillance";
            }
            else
            {
                _installPath = professionalInstallPath;
            }
        }

        public BasicCredential Decrypt(string encrypted)
        {
            var assembly = Assembly.LoadFile(Path.Combine(_installPath, "VideoOS.Administrator.Tools.NET.dll"));

            var cryptoPp = assembly.GetType("VideoOS.Common.Tools.CryptoDecoderStatics");
            var decryptor = cryptoPp.GetMethod("DecryptFromHexString");
            var decrypted = decryptor.Invoke(null, new object[] { encrypted }) as string;


            if (string.IsNullOrEmpty(decrypted))
            {
                return new BasicCredential();
            }
            var credentialParts = decrypted.Split(new[] { ':' }, 2);
            if (credentialParts.Length != 2)
                return credentialParts.Length == 1
                    ? new BasicCredential() {Username = credentialParts[0], Password = credentialParts[0]}
                    : new BasicCredential() {Username = "root", Password = "pass"};
            return new BasicCredential {Username = credentialParts[0], Password = credentialParts[1]};
        }
    }

    internal struct BasicCredential
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
