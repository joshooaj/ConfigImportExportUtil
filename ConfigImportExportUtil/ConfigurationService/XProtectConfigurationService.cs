using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using VideoOS.ConfigurationAPI;
using VideoOS.Platform.Util;

namespace ConfigImportExportUtil.ConfigurationService
{
    internal class XProtectConfigurationService
    {
        private readonly Uri _uri;

        private readonly object _instanceLock = new object();
        private IConfigurationService _instance;
        public IConfigurationService Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    _instance = _instance ?? GetConfigurationServiceContext(_uri);
                }

                return _instance;
            }
        }

        public XProtectConfigurationService(Uri uri)
        {
            _uri = uri;
        }
        private IConfigurationService GetConfigurationServiceContext(Uri uri)
        {
            var newUri = new Uri($"http://{uri.Host}:{uri.Port}/ManagementServer/ConfigurationApiService.svc");
            var binding = GetBinding(false, true);
            var spn = SpnFactory.GetSpn(newUri);
            var endpoint = new EndpointAddress(newUri, EndpointIdentity.CreateSpnIdentity(spn));
            var channelFactory = new ChannelFactory<IConfigurationService>(binding, endpoint);
            var clientTokenHelper = new ClientTokenHelper(newUri.Host);
            channelFactory.Endpoint.Behaviors.Add(new TokenServiceBehavior(clientTokenHelper));
            channelFactory.Credentials.Windows.ClientCredential = CredentialCache.DefaultNetworkCredentials;
            return channelFactory.CreateChannel();
        }

        internal static Binding GetBinding(bool isBasic, bool isCorporate)
        {
            if (!isBasic)
            {
                var binding = new WSHttpBinding();
                binding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
                binding.ReaderQuotas.MaxStringContentLength = 2147483647;
                binding.MaxReceivedMessageSize = 2147483647;
                binding.MaxBufferPoolSize = 2147483647;
                binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
                return binding;
            }
            else
            {
                var binding = new BasicHttpBinding();
                binding.ReaderQuotas.MaxStringContentLength = 2147483647;
                binding.MaxReceivedMessageSize = 2147483647;
                binding.MaxBufferSize = 2147483647;
                binding.MaxBufferPoolSize = 2147483647;
                binding.HostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
                binding.MessageEncoding = WSMessageEncoding.Text;
                binding.TextEncoding = Encoding.UTF8;
                binding.UseDefaultWebProxy = true;
                binding.AllowCookies = false;
                binding.Namespace = "VideoOS.ConfigurationAPI";
                if (isCorporate)
                {
                    binding.Security.Mode = BasicHttpSecurityMode.Transport;
                    binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
                }
                else
                {
                    binding.Security.Mode = BasicHttpSecurityMode.None;
                }
                return binding;
            }
        }
    }
}