#!/bin/bash
# Script to initialize SQL Server database
# This should be run after SQL Server container is healthy

echo "Waiting for SQL Server to be ready..."
sleep 10

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Password123!' -C -Q "
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProductsDb')
BEGIN
    CREATE DATABASE ProductsDb;
    PRINT 'Database ProductsDb created successfully';
END
ELSE
BEGIN
    PRINT 'Database ProductsDb already exists';
END
"

