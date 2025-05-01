# Production Setup

This directory contains the production-ready configuration for running the application using Docker Compose.

## Getting Started

1. Create a `.env` file in this directory with the following variables:

```env
# Redis Configuration
REDIS_PASSWORD=your_secure_redis_password
REDIS_CONNECTION=redis:6379,password=${REDIS_PASSWORD}

# Port Configuration
API_PORT=9002
WEBSOCKET_PORT=9003
```

2. Replace `your_secure_redis_password` with a strong password of your choice.

3. Adjust the ports if needed (default: API on 9002, WebSocket on 9003).

## Running the Application

To start all services:

```bash
docker-compose up -d
```

This will start:
- Client service
- Core API service
- WebSocket service
- Background service
- Redis instance

## Services

- **Client Service**: Web interface accessible on port 9000
- **Core API**: Accessible on port defined by `API_PORT`
- **WebSocket**: Accessible on port defined by `WEBSOCKET_PORT`
- **Background Service**: Runs internal tasks
- **Redis**: Used for data storage and communication between services

## Volumes

The setup includes two persistent volumes:
- `redis-data`: For Redis data persistence
- `shared-data`: For shared files between services

## Client Configuration

The client service requires an `nginx.conf` file in the same directory as the docker-compose.yml. This file should contain the Nginx configuration for the client application.

## Security Notes

- Always use strong passwords for Redis
- Keep your `.env` file secure and never commit it to version control
- Consider using a reverse proxy in front of the services for additional security
- Ensure your nginx.conf is properly configured for production use 