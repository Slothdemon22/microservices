# Microservices Cursor Rules

## Global Configuration
- **Language**: dotnet
- **Communication**: RabbitMQ
- **Action**: Log only (no business logic or side-effects)

---

## Services

### 1. order-service
- Language: dotnet  
- Runs on: port `3001`  
- Database: PostgreSQL (separate instance)  
- DB Port: `TBD`  
- Publishes to: RabbitMQ  
- Logs messages: ✅

### 2. notification-service
- Language: dotnet  
- Runs on: port `3000`  
- Database: PostgreSQL (separate instance)  
- DB Port: `TBD`  
- Consumes from: RabbitMQ  
- Logs messages: ✅

---

## RabbitMQ
- Required: ✅  
- Port: `5672`  
- Management Port: `15672`

---

## Rules
- ❌ **No DB sharing** between services  
- ✅ **Isolation** enforced  
- ✅ **Only RabbitMQ** for inter-service communication  
- ✅ **Only logging**, no actual data processing  
- ⚠️ Awaiting PostgreSQL exposed ports to complete configuration

