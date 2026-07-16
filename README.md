# Poliedro Hangfire - Automatización de Facturación

Servicio de automatización de facturación electrónica para Poliedro usando Hangfire. El servicio obtiene clientes desde una API externa, registra jobs recurrentes por cliente, consulta facturas pendientes y las envía para emisión.

## Stack

- .NET 10 (ASP.NET Core)
- Hangfire con MySQL (MySqlStorage)
- Docker / AWS ECS

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MySQL (según `appsettings.json` o variable `MYSQL_CONNECTION`)

## Ejecutar localmente

```bash
dotnet restore
dotnet build
dotnet run --project PoliedroHangFire
```

La app escucha en `http://localhost:5254` y `https://localhost:7008`.

## Variables de entorno

| Variable | Descripción |
|---|---|
| `MYSQL_CONNECTION` | Connection string de MySQL (si no se define, usa `appsettings.json`) |

## Endpoints

| Ruta | Descripción |
|---|---|
| `/hangfire` | Dashboard de Hangfire (sin auth) |
| `/health` | Health check detallado (JSON) |
| `/health/simple` | Health check simple (usado por Docker) |

## Jobs

Al iniciar, la app obtiene los clientes desde `External:ClientsUrl` y registra un job recurrente por cliente (`facturacion-cliente-{id}`). Cada job consulta facturas pendientes; si encuentra, encola un job de emisión.

## Docker

### Build local

```bash
docker build -t poliedro-hangfire .
```

### Run local

```bash
docker run -d -p 8080:8080 \
  -e MYSQL_CONNECTION="Server=tu-host;Port=3306;Database=tu-db;Uid=tu-user;Pwd=tu-pass;Allow User Variables=True" \
  --name poliedro-hangfire poliedro-hangfire
```

### Ver logs

```bash
docker logs -f poliedro-hangfire
```

### Verificar health

```bash
curl http://localhost:8080/health/simple
```

### Detener y eliminar

```bash
docker stop poliedro-hangfire
docker rm poliedro-hangfire
```

## CI/CD

GitHub Actions (`.github/workflows/aws.yml`) ejecuta build, test, SonarCloud, y en merge a `main`/`release/*`/`releasecandidate/*` publica imagen Docker en ECR y Docker Hub, y despliega en AWS ECS.

## Estructura del proyecto

```
PoliedroHangFire/
├── WebApi/                  Program.cs, health checks, filtros
├── Domain/                  Entidades (ClientBilling)
├── Application/             Interfaces (Client, PendingInvoices, Emitter)
├── Infrastructure/          Adaptadores HTTP a APIs externas
└── HangfireJobs/            Registro de jobs recurrentes
```

**Nota:** Los directorios `Infrastructure/Persistence/` e `Infrastructure/Resource/` están excluidos de compilación en el `.csproj`.
