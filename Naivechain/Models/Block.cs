using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Naivechain.Models
{
    class Block
    {
        public int Index { get; set; }
        public string PreviousHash { get; set; }
        public long Timestamp { get; set; }
        public string Data { get; set; }
        public string Hash { get; set; }

        public Block()
        {

        }

        public Block(int index, string previousHash, long timestamp, string data, string hash)
        {
            Index = index;
            PreviousHash = previousHash;
            Timestamp = timestamp;
            Data = data;
            Hash = hash;
        }

        public override bool Equals(object obj)
        {
            Block block = obj as Block;
            if(block == null)
            {
                return false;
            }

            return block.Hash.Equals(Hash);
        }

        public override int GetHashCode()
        {
            return this.Hash.GetHashCode();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
