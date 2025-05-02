# WoolBall-Server

## How to use

### Quick Start

```bash
git clone https://github.com/woolball-xyz/browser-network-server.git
cd browser-network-server/usage
docker compose up -d
```
> Once the services are running, you can access the Client at `http://localhost:9000`

[![Deploy to DO](https://www.deploytodo.com/do-btn-blue.svg)](https://cloud.digitalocean.com/apps/new?repo=https://github.com/woolball-xyz/browser-network-server/tree/deploy)


### Local Development

For local development, you must use Docker Compose as the services depend on a shared volume for proper operation:

```bash
git clone https://github.com/woolball-xyz/browser-network-server.git
cd browser-network-server
docker compose up --build -d
```

The services will run on their respective ports:
- WebSocket Service: Port 9003
- API Service: Port 9002

### Flow

![Current Network Status](current.png)
