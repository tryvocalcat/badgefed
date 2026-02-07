using BadgeFed.Core;
using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class RelayActorService
    {
        public async Task SetupRelayActorForDomain(string domain)
        {
            var defaultUsername = "_relaybot";
            
            Console.WriteLine($"Setting up default actor for domain: {domain}");
            
            var localDbService = new LocalScopedDb(domain);
            var existingActor = localDbService.GetActorByFilter($"Username = \"{defaultUsername}\" AND Domain = \"{domain}\"");
            
            if (existingActor == null)
            {
                // Generate public/private key pair
                var keyPair = await CryptoService.GenerateKeyPairAsync();

                // Create the default actor
                var defaultActor = new Actor
                {
                    FullName = $"{domain.ToTitleCase()} Relay Bot",
                    Username = defaultUsername,
                    Domain = domain,
                    Summary = $"Official relay bot for badge announcements on {domain}. Automatically boosts badge grants and credential updates to help spread the word about achievements across the ActivityPub network.",
                    PublicKeyPem = keyPair.PublicKeyPem,
                    PrivateKeyPem = keyPair.PrivateKeyPem,
                    InformationUri = $"https://{domain}/about",
                    AvatarPath = "img/defaultavatar.png",
                    IsMain = true
                };

                localDbService.UpsertActor(defaultActor);
                Console.WriteLine($"Default actor created with username '{defaultUsername}' and domain '{domain}'.");
            }
        }
    }
}