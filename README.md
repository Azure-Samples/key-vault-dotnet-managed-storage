---
services: key-vault
platforms: dotnet
author: dragav
---

# .Net SDK samples illustrating the management and consumption of Azure Key Vault-managed storage account keys.  

This repo contains sample code demonstrating the management and consumption of Azure Storage account keys via Azure Key Vault, using the [Azure .Net SDK](https://docs.microsoft.com/en-us/dotnet/api/overview/azure/key-vault?view=azure-dotnet). The scenarios covered by these samples include:

* Setting up and managing a storage account in a key vault: adding, removing, backing up, restoring and recovering an account
* Setting up and managing SAS token definitions in a key vault: adding, removing, backing up, restoring and recovering a SAS definition
* Consuming a SAS token - obtaining it from Azure Key Vault, and using it to access an Azure Storage endpoint

Support for Azure Key Vault-managed storage account may be colloquially referred to as 'MSAK'; SAS stands for shared access signature. We assume reader familiarity with [Azure Storage](https://docs.microsoft.com/en-us/azure/storage/) in general, and [SAS tokens](https://docs.microsoft.com/en-us/azure/storage/common/storage-dotnet-shared-access-signature-part-1) in particular. 

## Samples in this repo:

* Add and delete a managed storage account
* List and get existing managed storage accounts
* Backup and restore, delete and recover a managed storage account; permanently delete a managed storage account in a soft-delete enabled vault
* Set the storage account key regeneration period
* Rotate on demand the active storage account key 
* Add and delete a managed storage SAS definition; recover a deleted managed storage SAS definition
* List and get existing managed storage SAS definitions
* Obtain a SAS token from Azure Key Vault and use it to access an Azure Storage endpoint

## Getting Started

### Prerequisites

- OS: Windows
- SDKs:
    - KeyVault data SDK: Microsoft.Azure.KeyVault ver. 3.0.0+
- Azure:
    - an active Azure subscription, in which you have the Key Vault Contributor role
	- an Azure key vault
    - an Azure Active Directory application, created in the tenant associated with the subscription, and with access to KeyVault; please see [Accessing Key Vault from a native application](https://blogs.technet.microsoft.com/kv/2016/09/17/accessing-key-vault-from-a-native-application) for details.
    - the credentials of the AAD application, in the form of a client secret
    - an Azure Storage account, which you have access to (for data and management)
    - a user account, with List and Manage permissions to the storage account
    

### Installation

- open the solution in Visual Studio - NuGet should resolve the necessary packages

### Quickstart
Follow these steps to get started with this sample:

1. git clone https://github.com/Azure-Samples/key-vault-dotnet-managed-storage.git
2. cd key-vault-dotnet-managed-storage
4. edit the app.config file, specifying the tenant, subscription, AD app id and secret, and storage account and its resource id
5. dotnet run --project AzureKeyVaultManagedStorageSamples\AzureKeyVaultManagedStorageSamples.csproj

Note that storage account management requires a user account, and that the sample will interactively ask for a user login. 

## Demo


## Resources

Please see the following links for additional information:

- [Azure Storage overview](https://docs.microsoft.com/en-us/azure/storage/)
- [Azure Storage SAS tokens](https://docs.microsoft.com/en-us/azure/storage/common/storage-dotnet-shared-access-signature-part-1)
- [Azure Key Vault-managed storage accounts](https://docs.microsoft.com/en-us/azure/key-vault/key-vault-ovw-storage-keys)
- [Azure Key Vault PowerShell reference](https://docs.microsoft.com/en-us/powershell/module/azurerm.keyvault/?view=azurermps-5.7.0)
- [Azure Key Vault CLI reference](https://docs.microsoft.com/en-us/cli/azure/keyvault?view=azure-cli-latest)

The following samples are also related:

- []()
