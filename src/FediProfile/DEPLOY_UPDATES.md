# FediProfile Azure Deployment - Quick Reference

## Variables (customize these)
```powershell
$ResourceGroup = "rg-badgefed"
$WebAppName = "fediprofile-app"  # Change to your app name
$ProjectPath = "c:\Users\mahop\repos\badgefed\src\FediProfile"
```

## Option 1: Quick Deploy Script (Recommended)
```powershell
# PowerShell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
.\deploy-azure-update.ps1

# Bash
./deploy-azure-update.sh
```

## Option 2: Inline One-Liner (PowerShell)
```powershell
cd c:\Users\mahop\repos\badgefed\src\FediProfile; dotnet clean; dotnet build -c Release; dotnet publish -c Release -o .\bin\Release\net9.0\publish --no-build; Compress-Archive -Path ".\bin\Release\net9.0\publish\*" -DestinationPath ".\deploy.zip" -Force; az webapp deploy --name fediprofile-app --resource-group rg-badgefed --src-path .\deploy.zip --type zip; az webapp restart --name fediprofile-app --resource-group rg-badgefed; Write-Host "Deployed!" -ForegroundColor Green
```

## Option 3: Fastest Deploy (Skip build if only publishing)
```powershell
cd c:\Users\mahop\repos\badgefed\src\FediProfile
dotnet publish -c Release -o .\publish --no-build --no-restore
Compress-Archive -Path ".\publish\*" -DestinationPath ".\deploy.zip" -Force
az webapp deploy --name fediprofile-app --resource-group rg-badgefed --src-path .\deploy.zip --type zip
az webapp restart --name fediprofile-app --resource-group rg-badgefed
```

## Option 4: Using GitHub Actions (for CI/CD)
Create `.github/workflows/deploy-azure.yml`:
```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - run: |
          cd src/FediProfile
          dotnet publish -c Release -o publish
          zip -r deploy.zip publish/*
      - uses: azure/webapps-deploy@v2
        with:
          app-name: fediprofile-app
          package: src/FediProfile/deploy.zip
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
```

## Check Deployment Status
```powershell
# View live logs
az webapp log tail --name fediprofile-app --resource-group rg-badgefed --follow

# View recent logs
az webapp log download --name fediprofile-app --resource-group rg-badgefed --log-file logs.zip

# Check app status
az webapp show --name fediprofile-app --resource-group rg-badgefed --query "{State: state, Plan: appServicePlanId}" -o table

# Test endpoint
curl https://fediprofile-app.azurewebsites.net/profile
curl -H "Accept: application/activity+json" https://fediprofile-app.azurewebsites.net/profile
```

## Rollback to Previous Version
```powershell
# List deployment history
az webapp deployment list --name fediprofile-app --resource-group rg-badgefed --output table

# Restart to previous version
az webapp restart --name fediprofile-app --resource-group rg-badgefed
```

## Workflow for Regular Updates
1. Make code changes locally
2. Test: `dotnet run`
3. Commit and push to git
4. Deploy to Azure: `.\deploy-azure-update.ps1`
5. Check logs: `az webapp log tail --name fediprofile-app --resource-group rg-badgefed`
6. Verify: `curl https://fediprofile-app.azurewebsites.net/profile`

## Performance Tips
- Use `--no-build` and `--no-restore` flags if only republishing
- Skip `dotnet clean` if space isn't an issue (faster)
- Create a zip archive locally, then deploy just the zip
- Use `--quiet` flag for cleaner output in automation

## Troubleshooting
- App won't start: Check logs with `az webapp log tail`
- Database errors: Verify `/home/site/data` directory exists
- Settings not applied: Restart app after changing settings
- Slow deployment: Check your internet speed and Azure region load
