/*
 * Copyright 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security.Jwt;

namespace IdentityServer3.AccessTokenValidation
{
    internal class DiscoveryDocumentIssuerSecurityTokenProvider : IIssuerSecurityKeyProvider
    {
        private readonly string _discoveryEndpoint;
        private readonly HttpMessageHandler _handler;
        private readonly ILogger _logger;
        private readonly IdentityServerBearerTokenAuthenticationOptions _options;
        private readonly ReaderWriterLockSlim _synclock = new ReaderWriterLockSlim();
        private DateTime _lastConfigUpdate = DateTime.MinValue;
        private OpenIdConnectConfiguration _openIdConnectConfiguration;

        public DiscoveryDocumentIssuerSecurityTokenProvider(string discoveryEndpoint,
            IdentityServerBearerTokenAuthenticationOptions options, ILoggerFactory loggerFactory)
        {
            _discoveryEndpoint = discoveryEndpoint;
            _options = options;
            _logger = loggerFactory.Create(GetType().FullName);

            _handler = options.BackchannelHttpHandler ?? new WebRequestHandler();

            if (options.BackchannelCertificateValidator != null)
            {
                // Set the cert validate callback
                if (!(_handler is WebRequestHandler webRequestHandler))
                    throw new InvalidOperationException(
                        "The back channel handler must derive from WebRequestHandler in order to use a certificate validator");
                webRequestHandler.ServerCertificateValidationCallback =
                    options.BackchannelCertificateValidator.Validate;
            }

            if (!options.DelayLoadMetadata) RetrieveMetadata();
        }

        /// <value>
        ///     The identity server default audience
        /// </value>
        public string Audience
        {
            get
            {
                RetrieveMetadata();
                _synclock.EnterReadLock();
                try
                {
                    var issuer = _openIdConnectConfiguration.Issuer.EnsureTrailingSlash();
                    return issuer + "resources";
                }
                finally
                {
                    _synclock.ExitReadLock();
                }
            }
        }

        /// <summary>
        ///     Gets the issuer the credentials are for.
        /// </summary>
        /// <value>
        ///     The issuer the credentials are for.
        /// </value>
        public string Issuer
        {
            get
            {
                RetrieveMetadata();
                _synclock.EnterReadLock();
                try
                {
                    return _openIdConnectConfiguration.Issuer;
                }
                finally
                {
                    _synclock.ExitReadLock();
                }
            }
        }

        /// <summary>
        ///     Gets all known security keys.
        /// </summary>
        /// <value>
        ///     All known security keys.
        /// </value>
        public IEnumerable<SecurityKey> SecurityKeys
        {
            get
            {
                RetrieveMetadata();
                _synclock.EnterReadLock();
                try
                {
                    return _openIdConnectConfiguration.SigningKeys;
                }
                finally
                {
                    _synclock.ExitReadLock();
                }
            }
        }

        private void RetrieveMetadata()
        {
            _synclock.EnterWriteLock();
            try
            {
                var now = DateTime.Now;
                if (_openIdConnectConfiguration == null ||
                    _lastConfigUpdate - now >= _options.AutomaticRefreshInterval)
                {
                    _openIdConnectConfiguration = AsyncHelper.RunSync(async () =>
                        await OpenIdConnectConfigurationRetriever.GetAsync(_discoveryEndpoint, new HttpClient(_handler),
                            CancellationToken.None));
                    _lastConfigUpdate = now;
                }

                if (_openIdConnectConfiguration.SigningKeys == null)
                {
                    _logger.WriteError("Discovery document has no configured signing key. aborting.");
                    throw new InvalidOperationException("Discovery document has no configured signing key. aborting.");
                }
            }
            catch (Exception ex)
            {
                _logger.WriteError("Error contacting discovery endpoint: " + ex);
                throw;
            }
            finally
            {
                _synclock.ExitWriteLock();
            }
        }
    }
}