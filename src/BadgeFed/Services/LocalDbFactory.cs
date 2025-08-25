using System;

namespace BadgeFed.Services
{
    using System.Collections.Concurrent;

    public class LocalDbFactory
    {
        private static readonly ConcurrentDictionary<string, LocalDbService> _instances = new();

        public LocalDbService GetInstance(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var domain = uri.Host;
            return GetInstance(domain);
        }

        public LocalDbService GetInstance(HttpContext httpContext)
        {
            var domain = httpContext.Request.Host.Host;
            return GetInstance(domain);
        }

        public LocalDbService GetInstance(string domain)
        {
            // normalize
            domain = domain.Trim().ToLowerInvariant().TrimEnd('/').Replace(":", "_");

            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain must not be null or empty.", nameof(domain));

            return _instances.GetOrAdd(domain, d => new LocalDbService($"{d}.db"));
        }
    }
}