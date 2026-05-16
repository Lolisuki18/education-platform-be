@echo off
set /p mg_name="Enter Migration name (eg: AddNewTable): "

echo --- Creating Migration: %mg_name% ---
dotnet ef migrations add %mg_name% --project src/Infrastructure --startup-project src/API

echo.
echo --- Most updated Database ---
dotnet ef database update --project src/Infrastructure --startup-project src/API

echo.
echo --- Cheers! ---
pause.