# Naivechain

### Simple blockchain implementation
Naivechain is a C# implementation of a blockchain based on the original [Naivechain](https://github.com/lhartikk/naivechain). This blockchain deals only with storage of data, which makes it simple to understand and provides a good starting point to learn about blockchain.


### What is a blockchain
A blockchain is simply a chain of blocks. A block consists of a timestamp, some data, and a cryptographic hash of the previous block. Nodes in a network maintain a copy of the blockchain, and communicate with each other to add or verify new blocks to the chain.

Some blockchains, such as the blockchain used in the Bitcoin network, use a proof-of-work (PoW) function to achieve consensus between nodes and help secure the network. This blockchain implementation does not use proof-of-work, however.


### Overview of Naivechain
There are two main components to this project. The blockchain itself (and the operations around the management of the blockchain), and the networking capabilities. 

The blockchain is simply stored as a list of blocks. A Block is a data structure (defined in `Models/Block.cs`) that contains information about the block and some string data. The following diagram shows the blockchain structure.

![alt tag](blockchain.png)

The networking part of this project deals with the P2P communication between nodes as well as the HTTP communication that a user interfaces with. 


### P2P TCP Communication
Nodes communicate with each other to ensure that each node maintains an up-to-date, valid copy of the blockchain. Naivechain uses the `TcpListener` and `TcpClient` classes to allow P2P communication.  

For example, when a node has a new block added to it, the node broadcasts the block to each of its connected peers. If those peers deem the block to be valid, they will add the block to their blockchain. 


### HTTP Endpoints
Each node also exposes a HTTP server that allows users to interface with the node and the blockchain. Four endpoints are available:
* `GET /blocks/` - returns a JSON-encoded representation of each block (the whole blockchain)
* `POST /mineBlock/` - mines/creates a new block that contains the POST body as data and appends it to the nodes blockchain. The node then broadcasts the block to all other nodes it is connected to.
* `GET /peers/` - returns a list of all the peers that the node is connected to
* `POST /addPeer/` - connects the node to another node. Format: `address:port`


### Running
To clone, build and run this project:
```
git clone https://github.com/toastercoder/naivechain.git
cd naivechain/Naivechain
dotnet build
dotnet run <args>
```

3 command line arguments can be specified (none are required):
* `httpport=xxxx` – specify which port to start the HTTP server on (defaults to `3001` if not supplied)
* `p2pport=xxxx` – specify which port to start the TcpListener on (defaults to `6001` if not supplied)
* `peers=xxxxxx:xxxx` – specify initial peers for the node to connect to, comma seperated


### HTTP Requests
##### Get blockchain
```
curl http://localhost:3001/blocks/
```
##### Create block
```
curl -H "Content-type:application/json" --data '{"data" : "Some data to the first block"}' http://localhost:3001/mineBlock/
``` 
##### Add peer
```
curl -H "Content-type:application/json" --data '{"peer" : "ws://localhost:6001"}' http://localhost:3001/addPeer/
```
#### Query connected peers
```
curl http://localhost:3001/peers/
```
