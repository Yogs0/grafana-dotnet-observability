\# 🎯 Complete Monitoring Stack for .NET 8 API



A production-ready monitoring stack using Grafana, Prometheus, Loki, Tempo, OpenTelemetry, and Serilog for comprehensive observability of your .NET 8 applications.



\## 📦 What's Included



\### Monitoring Components

\- Grafana - Visualization and dashboarding

\- Prometheus - Metrics collection and storage

\- Loki - Log aggregation

\- Tempo - Distributed tracing

\- OpenTelemetry Collector - Telemetry data pipeline

\- Node Exporter - Host metrics (optional)



\### .NET 8 Integration

\- OpenTelemetry SDK for traces, metrics, and logs

\- Serilog for structured logging

\- Direct Loki integration

\- Prometheus metrics exporter

\- Full instrumentation for ASP.NET Core, Entity Framework, HTTP clients



\## 🚀 Quick Start



\### 1. Prerequisites

```bash

\# Required

\- Docker \& Docker Compose

\- .NET 8 SDK

\- 8+ GB RAM, 4+ CPU cores



\# Verify installations

docker --version

docker-compose --version

dotnet --version

```



\### 2. Clone and Setup

```bash

\# Create directory structure

mkdir monitoring-stack \&\& cd monitoring-stack



\# Copy all provided files to their respective directories

\# - docker-compose.yml (root)

\# - .env (root)

\# - prometheusprometheus.yml

\# - lokiloki-config.yml

\# - tempotempo.yml

\# - otel-collectorotel-collector-config.yml

\# - grafanaprovisioningdatasourcesdatasources.yml

\# - grafanaprovisioningdashboardsdashboards.yml



\# Set your admin password

nano .env

\# Change GRAFANA\_ADMIN\_PASSWORD=YourSecurePasswordHere

```



\### 3. Start Monitoring Stack

```bash

\# Start all services

docker-compose up -d



\# Check status

docker-compose ps



\# View logs

docker-compose logs -f

```



\### 4. Configure Your .NET 8 API



\#### Install NuGet Packages

```bash

dotnet add package OpenTelemetry --version 1.9.0

dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.9.0

dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore --version 1.9.0-beta.2

dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.9.0

dotnet add package OpenTelemetry.Instrumentation.Http --version 1.9.0

dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore --version 1.0.0-beta.12

dotnet add package Serilog.AspNetCore --version 8.0.2

dotnet add package Serilog.Sinks.Grafana.Loki --version 8.3.0

```



\#### Update Program.cs

Use the provided `dotnet-configProgram.cs` as a reference to add

\- OpenTelemetry configuration

\- Serilog with Loki sink

\- Prometheus metrics endpoint

\- Request logging



\#### Update appsettings.json

Copy configuration from `dotnet-configappsettings.json`



\### 5. Access Grafana

1\. Open `httplocalhost3000`

2\. Login

&nbsp;  - Username `admin`

&nbsp;  - Password (from `.env` file)

3\. All datasources are pre-configured!



\### 6. Import Dashboards



\#### Recommended Dashboards (Import by ID)

1\. ASP.NET Core - ID `10915`

&nbsp;  - Go to Dashboards → Import → Enter ID 10915

&nbsp;  

2\. .NET Runtime - ID `19924`

&nbsp;  - Comprehensive .NET runtime metrics



3\. Node Exporter - ID `1860`

&nbsp;  - Host system metrics



\#### Custom Dashboards

Create custom dashboards for

\- Application-specific metrics

\- Business KPIs

\- Custom traces and logs



\## 📊 Accessing Components



&nbsp;Component  URL  Purpose 

-------------------------

&nbsp;Grafana  httplocalhost3000  Main UI for visualization 

&nbsp;Prometheus  httplocalhost9090  Metrics exploration 

&nbsp;Loki  httplocalhost3100  Logs (via Grafana) 

&nbsp;Tempo  httplocalhost3200  Traces (via Grafana) 

&nbsp;OTel Collector Health  httplocalhost13133  Health check 

&nbsp;API Metrics  httplocalhost5000metrics  Prometheus scrape endpoint 



\## 🔍 Verify Everything Works



\### Check Telemetry Flow



\#### 1. Metrics

```bash

\# Check if Prometheus is scraping your API

curl httplocalhost9090apiv1targets



\# Check API metrics endpoint

curl httplocalhost5000metrics

```



\#### 2. Logs

In Grafana

1\. Go to Explore

2\. Select Loki datasource

3\. Query `{app=dotnet-api}`



\#### 3. Traces

In Grafana

1\. Go to Explore

2\. Select Tempo datasource

3\. Search for recent traces



\## 📈 Key Metrics to Monitor



\### Application Performance

```promql

\# Request rate

rate(http\_server\_request\_duration\_seconds\_count\[5m])



\# Average response time

rate(http\_server\_request\_duration\_seconds\_sum\[5m])  

rate(http\_server\_request\_duration\_seconds\_count\[5m])



\# 95th percentile latency

histogram\_quantile(0.95, rate(http\_server\_request\_duration\_seconds\_bucket\[5m]))



\# Error rate

rate(http\_server\_request\_duration\_seconds\_count{http\_response\_status\_code=~5..}\[5m])

```



\### .NET Runtime

```promql

\# GC collections

rate(dotnet\_gc\_collection\_count\_total\[5m])



\# Memory usage

process\_working\_set\_bytes



\# Thread count

dotnet\_threadpool\_num\_threads

```



\### Database (Entity Framework)

```promql

\# Query duration

rate(ef\_core\_database\_command\_duration\_seconds\_sum\[5m])  

rate(ef\_core\_database\_command\_duration\_seconds\_count\[5m])



\# Query count

rate(ef\_core\_database\_command\_duration\_seconds\_count\[5m])

```



\## 🔎 Log Queries (LogQL)



```logql

\# All logs from your API

{app=dotnet-api}



\# Error logs only

{app=dotnet-api} = level=Error



\# Logs for specific endpoint

{app=dotnet-api}  json  RequestPath=apiusers



\# Slow requests (1 second)

{app=dotnet-api}  json  Duration  1000



\# Logs with trace ID

{app=dotnet-api}  json  TraceId!=

```



\## 🏗️ Directory Structure



```

monitoring-stack

├── docker-compose.yml

├── .env

├── DEPLOYMENT-GUIDE.md

├── README.md

├── grafana

│   ├── provisioning

│   │   ├── datasources

│   │   │   └── datasources.yml

│   │   └── dashboards

│   │       └── dashboards.yml

│   └── dashboards              # Place custom JSON dashboards here

├── prometheus

│   └── prometheus.yml

├── loki

│   └── loki-config.yml

├── tempo

│   └── tempo.yml

├── otel-collector

│   └── otel-collector-config.yml

└── dotnet-config               # Reference configuration

&nbsp;   ├── Program.cs

&nbsp;   ├── appsettings.json

&nbsp;   └── YourApiName.csproj

```



\## 🔧 Configuration Tips



\### Adjust Scrape Intervals

In `prometheusprometheus.yml`

```yaml

scrape\_configs

&nbsp; - job\_name 'dotnet-api'

&nbsp;   scrape\_interval 10s  # Adjust based on needs

```



\### Adjust Log Retention

In `lokiloki-config.yml`

```yaml

limits\_config

&nbsp; retention\_period 744h  # 31 days - adjust as needed

```



\### Adjust Trace Sampling

In `dotnet-configProgram.cs`

```csharp

.SetSampler(new TraceIdRatioBasedSampler(0.1))   Sample 10% of traces

```



\## 🚨 Common Issues \& Solutions



\### Issue Can't connect to monitoring services

```bash

\# Check if services are running

docker-compose ps



\# Check logs for errors

docker-compose logs \[service-name]



\# Restart services

docker-compose restart

```



\### Issue No metrics appearing in Prometheus

```bash

\# Check if API is exposing metrics

curl httplocalhost5000metrics



\# Check Prometheus targets

curl httplocalhost9090apiv1targets



\# Verify network (if separated deployment)

docker-compose exec prometheus ping host.docker.internal

```



\### Issue Logs not appearing in Loki

```bash

\# Test Loki endpoint

curl httplocalhost3100ready



\# Check OTel Collector logs

docker-compose logs otel-collector



\# Verify Serilog configuration in appsettings.json

```



\### Issue Traces not appearing in Tempo

```bash

\# Check Tempo health

curl httplocalhost3200ready



\# Verify OTel Collector is forwarding traces

docker-compose logs otel-collector  grep tempo



\# Check your API is sending traces

\# Look for OpenTelemetry initialization logs

```



\## 📚 Learn More



\- \[Full Deployment Guide](DEPLOYMENT-GUIDE.md) - Detailed architecture and deployment options

\- \[OpenTelemetry .NET](httpsopentelemetry.iodocslanguagesnet)

\- \[Grafana Documentation](httpsgrafana.comdocs)

\- \[Prometheus Best Practices](httpsprometheus.iodocspractices)

\- \[Loki LogQL](httpsgrafana.comdocslokilatestquery)



\## 🎯 Recommended Dashboard Setup



\### 1. Overview Dashboard

\- Request rate (line chart)

\- Error rate (line chart)

\- Response time p95 (line chart)

\- Active connections (gauge)

\- Top endpoints by traffic (table)



\### 2. Performance Dashboard

\- Response time by endpoint (heatmap)

\- Database query performance (line chart)

\- Slow queries (table)

\- Cache hit rate (line chart)



\### 3. Errors Dashboard

\- Error count by type (bar chart)

\- Recent errors (log panel)

\- Error rate by endpoint (table)

\- Stack traces (log panel)



\### 4. Infrastructure Dashboard

\- CPU usage (line chart)

\- Memory usage (line chart)

\- Disk IO (line chart)

\- Network traffic (line chart)



\### 5. Business Metrics Dashboard

\- Custom business events

\- User activity

\- Feature usage

\- Custom KPIs



\## 🔐 Security Checklist



\- \[ ] Change default Grafana password

\- \[ ] Enable HTTPS for Grafana

\- \[ ] Restrict network access (firewall rules)

\- \[ ] Use strong passwords in `.env`

\- \[ ] Keep Docker images updated

\- \[ ] Review retention policies

\- \[ ] Enable authentication for all services

\- \[ ] Use secrets management (e.g., Docker secrets)



\## 📈 Scaling Considerations



\### When to Scale Vertically (Bigger Machine)

\- Single API instance

\- Low to medium traffic

\- Simpler operations



\### When to Scale Horizontally (Multiple Machines)

\- Multiple API instances

\- High traffic (10k reqmin)

\- Need high availability

\- Running microservices



See \[DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md) for detailed scaling strategies.



\## 🛠️ Maintenance



\### Daily

```bash

\# Check disk usage

docker system df



\# Check running containers

docker-compose ps

```



\### Weekly

```bash

\# Update images

docker-compose pull



\# Restart with new images

docker-compose up -d



\# Clean up

docker system prune -f

```



\### Monthly

```bash

\# Backup Grafana dashboards

\# Review and adjust retention policies

\# Capacity planning review

```



\## 🤝 Contributing



Suggestions and improvements are welcome! Key areas

\- Additional dashboard templates

\- Performance optimizations

\- Security enhancements

\- Documentation improvements



\## 📝 License



This configuration is provided as-is for educational and production use.



\## 🆘 Support



For issues

1\. Check \[Common Issues](#-common-issues--solutions)

2\. Review logs `docker-compose logs \[service]`

3\. Consult \[DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md)

4\. Check official documentation



---



Happy Monitoring! 🚀📊



Built with ❤️ for .NET developers who care about observability.



