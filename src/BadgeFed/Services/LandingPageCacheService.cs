using System.Collections.Concurrent;
using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class LandingPageCacheService
    {
        private readonly ConcurrentDictionary<string, CachedLandingPage> _cache = new();
        
        public void Set(string domain, string htmlContent)
        {
            var cachedPage = new CachedLandingPage
            {
                HtmlContent = htmlContent,
                CachedAt = DateTime.UtcNow
            };
            
            _cache.AddOrUpdate(domain, cachedPage, (key, oldValue) => cachedPage);
        }
        
        public bool TryGetValue(string domain, out string htmlContent)
        {
            htmlContent = null;
            if (_cache.TryGetValue(domain, out var cachedPage))
            {
                htmlContent = cachedPage.HtmlContent;
                return true;
            }
            return false;
        }
        
        public void Remove(string domain)
        {
            _cache.TryRemove(domain, out _);
        }
        
        public void Clear()
        {
            _cache.Clear();
        }
        
        private class CachedLandingPage
        {
            public string HtmlContent { get; set; }
            public DateTime CachedAt { get; set; }
        }
    }
}
