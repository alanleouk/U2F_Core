﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using U2F.Core.Exceptions;
using U2F.Core.Utils;

namespace U2F.Core.Models
{
    public class RawAuthenticateResponse
    {
        private const byte UserPresentFlag = 0x01;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawAuthenticateResponse"/> class.
        /// </summary>
        /// <param name="userPresence">The user presence.</param>
        /// <param name="counter">The counter.</param>
        /// <param name="signature">The signature.</param>
        public RawAuthenticateResponse(byte userPresence, uint counter, byte[] signature)
        {
            UserPresence = userPresence;
            Counter = counter;
            Signature = signature;
        }

        /// <summary>
        /// Gets the user presence.
        /// Bit 0 is set to 1, which means that user presence was verified. (This
        /// version of the protocol doesn't specify a way to request authentication
        /// responses without requiring user presence.) A different value of bit 0, as
        /// well as bits 1 through 7, are reserved for future use. The values of bit 1
        /// through 7 SHOULD be 0
        /// </summary>
        /// <value>
        /// The user presence.
        /// </value>
        public byte UserPresence { get; private set; }

        /// <summary>
        /// Gets the counter.
        /// This is the big-endian representation of a counter value that the U2F device
        /// increments every time it performs an authentication operation.
        /// </summary>
        /// <value>
        /// The counter.
        /// </value>
        public uint Counter { get; private set; }

        /// <summary>
        /// Gets the signature.
        /// This is a ECDSA signature (on P-256)
        /// </summary>
        /// <value>
        /// The signature.
        /// </value>
        public byte[] Signature { get; private set; }

        /// <summary>
        /// Froms the base64.
        /// </summary>
        /// <param name="rawDataBase64">The raw data base64.</param>
        /// <returns></returns>
        public static RawAuthenticateResponse FromBase64(string rawDataBase64)
        {
            byte[] bytes = rawDataBase64.Base64StringToByteArray();

            Stream stream = new MemoryStream(bytes);
            BinaryReader binaryReader = new BinaryReader(stream);

            byte userPresence = binaryReader.ReadByte();
            byte[] counterBytes = binaryReader.ReadBytes(4);

            //counter has to be reversed if its little endian encoded
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            uint counter = BitConverter.ToUInt32(counterBytes, 0);

            long size = binaryReader.BaseStream.Length - binaryReader.BaseStream.Position;
            byte[] signature = binaryReader.ReadBytes((int)size);

            try
            {
                return new RawAuthenticateResponse(
                    userPresence,
                    counter,
                    signature);
            }
            finally
            {
                stream.Dispose();
                binaryReader.Dispose();
            }
        }

        /// <summary>
        /// Checks the signature.
        /// </summary>
        /// <param name="appId">The application identifier.</param>
        /// <param name="clientData">The client data.</param>
        /// <param name="publicKey">The public key.</param>
        public void CheckSignature(string appId, string clientData, byte[] publicKey)
        {
            byte[] signedBytes = PackBytesToSign(
                Crypto.U2F.Crypto.Hash(appId),
                UserPresence,
                Counter,
                Crypto.U2F.Crypto.Hash(clientData));

            Crypto.U2F.Crypto.CheckSignature(
                Crypto.U2F.Crypto.DecodePublicKey(publicKey),
                signedBytes,
                Signature);
        }

        /// <summary>
        /// Packs the bytes to sign.
        /// </summary>
        /// <param name="appIdHash">The application identifier hash.</param>
        /// <param name="userPresence">The user presence.</param>
        /// <param name="counter">The counter.</param>
        /// <param name="challengeHash">The challenge hash.</param>
        /// <returns></returns>
        public byte[] PackBytesToSign(byte[] appIdHash, byte userPresence, uint counter, byte[] challengeHash)
        {
            // covert the counter to a byte array in case the int is to big for a single byte
            byte[] counterBytes = BitConverter.GetBytes(counter);

            //counter has to be reversed if its little endian encoded
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            List<byte> someBytes = new List<byte>();
            someBytes.AddRange(appIdHash);
            someBytes.Add(userPresence);
            someBytes.AddRange(counterBytes);
            someBytes.AddRange(challengeHash);

            return someBytes.ToArray();
        }

        public void CheckUserPresence()
        {
            if (UserPresence != UserPresentFlag)
            {
                throw new U2fException("User presence invalid during authentication");
            }
        }

        public override int GetHashCode()
        {
            return 23 + Signature.Sum(b => b + 31 + (int)Counter + UserPresence);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RawAuthenticateResponse))
                return false;
            if (this == obj)
                return true;
            if (GetType() != obj.GetType())
                return false;
            RawAuthenticateResponse other = (RawAuthenticateResponse)obj;
            if (Counter != other.Counter)
                return false;
            
            if (!Signature.SequenceEqual(other.Signature))
                return false;
            return UserPresence == other.UserPresence;
        }
    }
}