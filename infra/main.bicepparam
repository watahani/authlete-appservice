using './main.bicep'

param environmentName = sys.readEnvironmentVariable('AZURE_ENV_NAME', 'dev')
param location = sys.readEnvironmentVariable('AZURE_LOCATION', 'japaneast')
param clientId = sys.readEnvironmentVariable('AUTHLETE_CLIENT_ID', '')
param clientSecret = sys.readEnvironmentVariable('AUTHLETE_CLIENT_SECRET', '')
param azureOpenAIKey = sys.readEnvironmentVariable('AZURE_OPENAI_KEY', '')
param azureOpenAIEndpoint = sys.readEnvironmentVariable('AZURE_OPENAI_ENDPOINT', '')

