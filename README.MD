# Take & Go — PoC

### 1. Run the docker commands to provision RabbitMQ & Postgre DB
docker compose -f infra.docker-compose.yml up

### 2. Run the following services
dotnet run --project src/Tag.Api --urls http://localhost:5109
dotnet run --project src/Tag.OutboxWorker
dotnet run --project src/Tag.Orchestrator
dotnet run --project src/Tag.DisplayRelay --urls http://localhost:5273

UI for display TV: http://localhost:5233/?siteId=SITE01&roomId=ROOMA


### 3. Test
Create a visit event:
curl -s -X POST http://localhost:5109/entry-scan \
  -H "content-type: application/json" \
  -d '{"siteId":"SITE01","roomId":"ROOMA","enlisteeId":"E123","enlisteeName":"Tan Wei Ming","packLocation":"B-12"}'

Take note of the visit event ID <VSID>
Display TV UI displays the visit event.

Create exit event:
curl -s -X POST http://localhost:5109/exit-scan \
  -H "content-type: application/json" \
  -d '{"visitSessionId":"<VSID>"}'

Event is removed from TV UI.

Happy hacking !

