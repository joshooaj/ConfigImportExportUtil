using VideoOS.ConfigurationAPI;

namespace ConfigImportExportUtil.ConfigurationService
{
    /// <summary>
    /// This class assist in adding the login token to the SOAP header
    /// </summary>
    public class ClientTokenHelper : TokenHelper
    {
        private readonly string _serverAddress;
        public ClientTokenHelper(string serverAddress)
        {
            _serverAddress = serverAddress;
        }
        public override string GetToken()
        {
            var ls = VideoOS.Platform.Login.LoginSettingsCache.GetLoginSettings(_serverAddress);
            return ls != null ? ls.Token : "";
        }
        public override bool ValidateToken(string token)
        {
            // Not used on client side
            return true;
        }
    }
}