using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Naivechain.Models;
using System.Text.Json;
using System.IO;

namespace Naivechain
{
    class Program
    {
        static int httpPort = 3001;
        static string httpBaseUrl = "http://localhost:" + httpPort + "/";
        static int p2pPort = 6001;
        static string p2pAddress = "127.0.0.1";

        static List<Block> blockchain = new List<Block>{ };
        static List<TcpClient> clients = new List<TcpClient>();
        static List<string> initialPeers = new List<string>();

        static TcpListener server = null;

        static void Main(string[] args)
        {
            // Parse command line arguments
            foreach(string arg in args)
            {
                if(arg.StartsWith("httpport=", StringComparison.OrdinalIgnoreCase))
                {
                    httpPort = Int32.Parse(arg.Split('=')[1]);
                    httpBaseUrl = "http://localhost:" + httpPort + "/";
                }
                else if(arg.StartsWith("p2pport=", StringComparison.OrdinalIgnoreCase))
                {
                    p2pPort = Int32.Parse(arg.Split('=')[1]);
                }
                else if(arg.StartsWith("peers=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] peers = arg.Split('=')[1].Split(',');
                    initialPeers.AddRange(peers);
                }
            }

            // Add genesis block to blockchain
            blockchain.Add(getGenesisBlock());

            // Start http and p2p servers, each on own threads
            startP2pServer();
            connectToPeers(initialPeers);

            new Thread(() => acceptP2pClients()).Start();
            new Thread(() => initHttpServer()).Start();
            
        }

        static Block getGenesisBlock()
        {
            return new Block(0, "0", 1594622763, "genesis block wooooooo", "f29637b749152cd03dfa0b39e8cd534701a8262f9c37ce8e16732570cecef47a");
        }


        static Block generateNextBlock(string blockData)
        {
            Block previousBlock = getLatestBlock();
            int nextIndex = previousBlock.Index + 1;
            long nextTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            string nextHash = calculateHash(nextIndex, previousBlock.Hash, nextTimestamp, blockData);

            return new Block(nextIndex, previousBlock.Hash, nextTimestamp, blockData, nextHash);
        }

        static string calculateHashForBlock(Block block)
        {
            return calculateHash(block.Index, block.PreviousHash, block.Timestamp, block.Data);
        }

        static string calculateHash(int index, string previousHash, long timestamp, string data)
        {
            SHA256 sha256 = SHA256.Create();
            string hash = String.Empty;

            string toHash = index.ToString() + previousHash + timestamp.ToString() + data;
            byte[] hashed = sha256.ComputeHash(Encoding.UTF8.GetBytes(toHash));

            foreach (byte b in hashed)
            {
                hash += b.ToString("x2");
            }

            return hash.ToString();
        }

        static void addBlock(Block newBlock)
        {
            if(isValidNewBlock(newBlock, getLatestBlock()))
            {
                blockchain.Add(newBlock);
            }
        }

        static bool isValidNewBlock(Block newBlock, Block previousBlock)
        {
            if (previousBlock.Index + 1 != newBlock.Index)
            {
                Console.WriteLine("Invalid index");
                return false;
            }
            else if (!previousBlock.Hash.Equals(newBlock.PreviousHash))
            {
                Console.WriteLine("Invalid previoushash");
                return false;
            }
            else if (!calculateHashForBlock(newBlock).Equals(newBlock.Hash))
            {
                Console.WriteLine("Invalid hash: " + calculateHashForBlock(newBlock) + " " + newBlock.Hash);
            }

            return true;
        }


        static bool isValidChain(List<Block> blockchainToValidate)
        {
            if (!blockchainToValidate[0].Equals(getGenesisBlock()))
            {
                return false;
            }

            List<Block> tempBlocks = new List<Block> { blockchainToValidate[0] };
            for(int i = 1; i < tempBlocks.Count; i++)
            {
                if(isValidNewBlock(blockchainToValidate[i], tempBlocks[i - 1]))
                {
                    tempBlocks.Add(blockchainToValidate[i]);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        static Block getLatestBlock()
        {
            return blockchain[blockchain.Count - 1];
        }

        static void initHttpServer()
        {
            // Start the HTTP Listener at specified directories
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add(String.Format(httpBaseUrl, "blocks/"));
            httpListener.Prefixes.Add(String.Format(httpBaseUrl, "mineBlock/"));
            httpListener.Prefixes.Add(String.Format(httpBaseUrl, "peers/"));
            httpListener.Prefixes.Add(String.Format(httpBaseUrl + "addPeer/"));

            httpListener.Start();
            Console.WriteLine("HTTP listening on port: " + httpPort);

            // Handle received requests
            while (true)
            {
                HttpListenerContext context = httpListener.GetContext();
                HttpListenerRequest request = context.Request;
                string requestUrl = request.Url.ToString();

                HttpListenerResponse response = context.Response;
                string responseString = string.Empty;

                if (requestUrl.Contains("/blocks"))
                {
                    responseString = JsonSerializer.Serialize(blockchain);
                }
                else if (requestUrl.Contains("/mineBlock"))
                {
                    if (request.HasEntityBody)
                    {
                        string reqBody;
                        using (Stream body = request.InputStream)
                        {
                            using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
                            {
                                reqBody = reader.ReadToEnd();
                            }
                        }

                        Block newBlock = generateNextBlock(reqBody);
                        addBlock(newBlock);

                        broadcast(responseLatestMsg());

                        Console.WriteLine("Block added: " + newBlock.ToString());
                        responseString = newBlock.ToString();

                    }
                }
                else if (requestUrl.Contains("/peers"))
                {
                    string peers = String.Empty;
                    foreach (TcpClient peer in clients)
                    {
                        var endPoint = (IPEndPoint)peer.Client.RemoteEndPoint;
                        string address = endPoint.Address.ToString();
                        string port = endPoint.Port.ToString();
                        peers += address + ":" + port;
                        peers += "\n";
                    }

                    responseString = peers;
                }
                else if (requestUrl.Contains("/addPeer"))
                {
                    if (request.HasEntityBody)
                    {
                        string reqBody;
                        using (Stream body = request.InputStream)
                        {
                            using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
                            {
                                reqBody = reader.ReadToEnd();
                            }
                        }

                        connectToPeers(new List<string> { reqBody });
                    }
                }

                // Write to output stream
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;

                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
        }

        static void startP2pServer()
        {
            if(server == null)
            {
                server = new TcpListener(IPAddress.Parse(p2pAddress), p2pPort);
                server.Start();
                Console.WriteLine("TCP P2P listening on port: " + p2pPort);
            }
        }

        static void acceptP2pClients()
        {
            // Listen for clients, and start a thread for each client
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                new Thread(() => connectionHandler(client)).Start();
            }
        }

        static void connectToPeers(List<string> newPeers)
        {
            foreach (string peer in newPeers)
            {
                string hostname = peer.Split(':')[0];
                int port = Int32.Parse(peer.Split(':')[1]);

                try
                {
                    TcpClient peerClient = new TcpClient(hostname, port);
                    new Thread(() => connectionHandler(peerClient)).Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Connection to peer " + peer + " failed.");
                }
            }
        }

        static void connectionHandler(TcpClient client)
        {
            Console.WriteLine("New client");
            clients.Add(client);

            write(client, queryChainLengthMsg());

            byte[] bytes = new byte[1024];
            string data = null;

            NetworkStream stream = client.GetStream();
            int i;

            try
            {
                // Read the message
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Received message: " + data);

                    // Handle the message
                    Message message = JsonSerializer.Deserialize<Message>(data);
                    MessageType messageType = message.Type;
                    if(messageType == MessageType.QUERY_LATEST)
                    {
                        write(client, responseLatestMsg());
                    }
                    else if(messageType == MessageType.QUERY_ALL)
                    {
                        write(client, responseChainMsg());
                    }
                    else if(messageType == MessageType.RESPONSE_BLOCKCHAIN)
                    {
                        handleBlockchainResponse(message.Data);
                    }
                    
                }
            }
            catch(Exception e)
            {
                // Handle errors
                Console.WriteLine(e);
                Console.WriteLine(e.Message);
                clients.Remove(client);
                string url = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                Console.WriteLine("Connection failed to peer: " + url);
            }

        }

        static void handleBlockchainResponse(List<Block> message)
        {
            List<Block> receivedBlocks = message.OrderBy(i => i.Index).ToList();
            Block latestBlockReceived = receivedBlocks[receivedBlocks.Count - 1];
            Block latestBlockHeld = getLatestBlock();

            if(latestBlockReceived.Index > latestBlockHeld.Index)
            {
                Console.WriteLine("Blockchain possibly behind. We got: " + latestBlockHeld.Index + " Peer got: " + latestBlockReceived.Index);
                if(latestBlockHeld.Hash.Equals(latestBlockReceived.PreviousHash))
                {
                    Console.WriteLine("We can append the received block to our chain.");
                    blockchain.Add(latestBlockReceived);

                    broadcast(responseLatestMsg());
                }
                else if (receivedBlocks.Count == 1)
                {
                    Console.WriteLine("We have to query the chain from our peers");

                    broadcast(queryAllMsg());
                }
                else
                {
                    Console.WriteLine("Received blockchain is longer than current blockchain");
                    replaceChain(receivedBlocks);
                }
            }
            else
            {
                Console.WriteLine("Received blockchain is not longer than current blockchain. Current blockchain is the longest chain, so do nothing.");
            }
        }

        static void replaceChain(List<Block> newBlocks)
        {
            if(isValidChain(newBlocks) && newBlocks.Count > blockchain.Count)
            {
                Console.WriteLine("Received blockchain is valid. Replacing current blockchain with received blockchain.");
                blockchain = newBlocks;

                broadcast(responseLatestMsg());
            }
            else
            {
                Console.WriteLine("Recevied blockchain invalid.");
            }
        }
      
        static string responseLatestMsg()
        {
            return new Message(MessageType.RESPONSE_BLOCKCHAIN, new List<Block> { getLatestBlock() }).ToString();
        }

        static string queryAllMsg()
        {
            return new Message(MessageType.QUERY_ALL, null).ToString();
        }

        static string queryChainLengthMsg()
        {
            return new Message(MessageType.QUERY_LATEST, null).ToString();
        }

        static string responseChainMsg()
        {
            return new Message(MessageType.RESPONSE_BLOCKCHAIN, blockchain).ToString();
        }

        static void write(TcpClient client, string message)
        {
            NetworkStream stream = client.GetStream();

            byte[] msg = System.Text.Encoding.ASCII.GetBytes(message);
            stream.Write(msg, 0, msg.Length);
        }

        static void broadcast(string message)
        {
            foreach(var client in clients)
            {
                write(client, message);
            }
        }
        
    }
}
