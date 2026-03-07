using System.Collections.Concurrent;

namespace FediProfile.Services
{
    /// <summary>
    /// LocalDbFactory provides multi-tenant, multi-user database support by creating
    /// typed DomainScopedDb and UserScopedDb instances for each domain and user.
    /// </summary>
    public class LocalDbFactory
    {
        private static readonly ConcurrentDictionary<string, LocalDbService> _instances = new();

        /// <summary>
        /// Get or create a domain database instance for the given URI.
        /// </summary>
        public DomainScopedDb GetInstance(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var domain = uri.Host;
            return GetInstance(domain);
        }

        /// <summary>
        /// Get or create a domain database instance for the given HttpContext.
        /// </summary>
        public DomainScopedDb GetInstance(HttpContext httpContext)
        {
            var domain = httpContext.Request.Host.Host;
            return GetInstance(domain);
        }

        /// <summary>
        /// Get or create a domain database instance for the given domain.
        /// Returns DomainScopedDb with domain-level schema (Users, Settings).
        /// </summary>
        public DomainScopedDb GetInstance(string domain)
        {
            // Normalize domain name and strip port
            domain = domain.Split(':')[0].Trim().ToLowerInvariant().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain must not be null or empty.", nameof(domain));

            return (DomainScopedDb)_instances.GetOrAdd(domain, d => new DomainScopedDb(d));
        }

        /// <summary>
        /// Get or create a user-specific database instance for the given domain and user slug.
        /// Returns UserScopedDb with user-level schema (Links, ActorKeys, Badges, Followers, etc.).
        /// </summary>
        public UserScopedDb GetInstance(string domain, string userSlug, bool autoCreate = true)
        {
            // Normalize domain and user slug, and strip port
            domain = domain.Split(':')[0].Trim().ToLowerInvariant().TrimEnd('/');
            userSlug = userSlug.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain must not be null or empty.", nameof(domain));

            if (string.IsNullOrWhiteSpace(userSlug))
                throw new ArgumentException("User slug must not be null or empty.", nameof(userSlug));

            var key = $"{domain}_{userSlug}";
            return (UserScopedDb)_instances.GetOrAdd(key, k => new UserScopedDb(domain, userSlug, autoCreate));
        }
    }
}
