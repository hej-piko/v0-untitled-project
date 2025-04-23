# Deploying to Azure App Service

This guide will help you deploy the Esports Tournament Bracketing System to Azure App Service, which is better suited for .NET applications than Vercel.

## Prerequisites

1. An Azure account - [Create a free account](https://azure.microsoft.com/free/)
2. Azure CLI installed - [Install Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
3. .NET 6.0 SDK installed - [Install .NET](https://dotnet.microsoft.com/download/dotnet/6.0)

## Steps to Deploy

### 1. Prepare Your Application

Make sure your application is ready for deployment:

\`\`\`bash
# Restore dependencies
dotnet restore

# Build the application
dotnet build --configuration Release
\`\`\`

### 2. Create Azure Resources

\`\`\`bash
# Login to Azure
az login

# Create a resource group
az group create --name EsportsTournamentGroup --location eastus

# Create an App Service plan
az appservice plan create --name EsportsTournamentPlan --resource-group EsportsTournamentGroup --sku F1

# Create a web app
az webapp create --name EsportsTournament --resource-group EsportsTournamentGroup --plan EsportsTournamentPlan --runtime "DOTNET|6.0"
\`\`\`

### 3. Configure Database Connection

\`\`\`bash
# Add database connection string to app settings
az webapp config appsettings set --name EsportsTournament --resource-group EsportsTournamentGroup --settings "DATABASE_URL=your_database_connection_string"
\`\`\`

### 4. Deploy Your Application

\`\`\`bash
# Publish the application
dotnet publish -c Release -o ./publish

# Deploy to Azure
az webapp deployment source config-zip --name EsportsTournament --resource-group EsportsTournamentGroup --src ./publish.zip
\`\`\`

### 5. Configure Custom Domain (Optional)

\`\`\`bash
# Add a custom domain
az webapp config hostname add --webapp-name EsportsTournament --resource-group EsportsTournamentGroup --hostname your-domain.com
\`\`\`

## Troubleshooting

If you encounter issues with your deployment:

1. Check the application logs:
   \`\`\`bash
   az webapp log tail --name EsportsTournament --resource-group EsportsTournamentGroup
   \`\`\`

2. Verify your connection strings and app settings:
   \`\`\`bash
   az webapp config appsettings list --name EsportsTournament --resource-group EsportsTournamentGroup
   \`\`\`

3. Make sure your database is accessible from Azure App Service.

## Additional Resources

- [Azure App Service Documentation](https://docs.microsoft.com/azure/app-service/)
- [Deploy ASP.NET Core apps to Azure App Service](https://docs.microsoft.com/azure/app-service/app-service-web-get-started-dotnet)
- [Configure ASP.NET Core apps for Azure App Service](https://docs.microsoft.com/azure/app-service/configure-language-dotnetcore)
