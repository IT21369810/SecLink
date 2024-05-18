using System;
using System.Security.Cryptography;
namespace SecLinkApp
{
    internal static class EcdhHelper
    {
        // Generate ECDH key pair
        public static (byte[] publicKey, ECDiffieHellman ecdh) GenerateKeyPair()
        {
            ECDiffieHellman ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            byte[] publicKey = ecdh.PublicKey.ToByteArray();
            Console.WriteLine("ECDH Key Pair Generated. Public Key ready to send.");
            return (publicKey, ecdh);
        }
    }
}
