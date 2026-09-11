using System;
using System.Security.Cryptography;
using AVTOKarta.Licensing;

namespace LicGen
{
    public static class Signer
    {
        public static string CreateLicenseFile(RSACryptoServiceProvider privateKey, LicenseInfo info)
        {
            byte[] payload = LicensePayload.GetBytes(info);
            byte[] signature = LicenseCrypto.Sign(privateKey, payload);
            return LicenseContainer.Build(
                Convert.ToBase64String(payload),
                Convert.ToBase64String(signature));
        }
    }
}