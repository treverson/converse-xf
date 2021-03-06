﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bitcoin.BIP39;
using Client;
using Common;
using Converse.Helpers;
using Converse.Services;
using Crypto;
using Google.Protobuf;
using Protocol;

namespace Converse.Tron
{
    public class Wallet : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ECKey ECKey { get; }
        public string Mnemonic { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }

        string _privateKeyHexString;
        public string PrivateKeyHexString
        {
            get
            {
                if (_privateKeyHexString != null)
                {
                    return _privateKeyHexString;
                }
                else if (ECKey != null)
                {
                    _privateKeyHexString = ECKey.GetPrivateKey().ToHexString();
                    return _privateKeyHexString;
                }

                return string.Empty;
            }
            set => _privateKeyHexString = value;
        }

        public byte[] PrivateKey { get { return PrivateKeyHexString.FromHexToByteArray(); } }

        string _address;
        public string Address
        {
            get
            {
                if (_address != null)
                {
                    return _address;
                }
                else if (ECKey?.Pub != null)
                {
                    _address = new WalletAddress(ECKey).ToString();
                    return _address;
                }
                else
                {
                    return string.Empty;
                }
            }

            set => _address = value;
        }

        public byte[] PublicKey => ECKey?.Pub?.GetEncoded();

        public Wallet()
        {
            ECKey = new ECKey();
        }

        public Wallet(byte[] privateKey)
        {
            ECKey = new ECKey(privateKey);
        }

        public Wallet(ECKey eCKey)
        {
            ECKey = eCKey;
        }

        public Wallet(string privData, bool isPrivateKey = false)
        {
            ECKey = isPrivateKey ? new ECKey(privData) : new ECKey(BIP39.GetSeedBytes(privData).Take(32).ToArray());
            Mnemonic = isPrivateKey ? string.Empty : privData;
        }

        public async Task<long> GetConverseTokenAmountAsync(TronConnection connection)
        {
            try
            {
                var account = await connection.Client.GetAccountAsync(new Account { Address = ByteString.CopyFrom(WalletAddress.Decode58Check(Address)) });
                return account.AssetV2.ContainsKey(AppConstants.TokenID) ? account.AssetV2[AppConstants.TokenID] : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public void SignTransaction(Transaction transaction, bool setTimestamp = true)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }
            if (ECKey == null)
            {
                throw new NullReferenceException("No valid ECKey");
            }

            if (setTimestamp)
            {
                transaction.SetTimestampToNow();
            }

            var hash = Sha256.Hash(transaction.RawData.ToByteArray());
            var signature = ECKey.Sign(hash);

            var contracts = transaction.RawData.Contract;
            foreach (var contract in contracts)
            {
                transaction.Signature.Add(ByteString.CopyFrom(signature));
            }
        }

        public byte[] Encrypt(string message, byte[] publicKey)
        {
            return ECKey.Encrypt(Encoding.UTF8.GetBytes(message), publicKey);
        }

        public byte[] Encrypt(byte[] data, byte[] publicKey)
        {
            return ECKey.Encrypt(data, publicKey);
        }

        public string DecryptToString(byte[] encryptedMessage, byte[] publicKey)
        {
            var decryptedBytes = ECKey.Decrypt(encryptedMessage, publicKey);

            if (decryptedBytes != null)
            {
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            return string.Empty;
        }

        public byte[] Decrypt(byte[] encryptedMessage, byte[] publicKey)
        {
            try
            {
                var decryptedBytes = ECKey.Decrypt(encryptedMessage, publicKey); 
                return decryptedBytes;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
