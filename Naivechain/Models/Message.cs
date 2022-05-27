using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Naivechain.Models
{
    class Message
    {
        public MessageType Type { get; set; }
        public List<Block> Data { get; set; }

        public Message()
        {

        }

        public Message(MessageType type, List<Block> data)
        {
            Type = type;
            Data = data;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
