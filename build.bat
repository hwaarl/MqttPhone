@echo off

if exist DeploymentPackage rd /s /q DeploymentPackage
md DeploymentPackage\APK

echo ========================================================================================
echo Building MqttPhone...

cd MqttPhone
dotnet publish --configuration Release --runtime android-x64 --framework net10.0-android -p:PackageFormat=Apk
for /f "delims=" %%i in ('dir /s /b *-Signed.apk*') do copy "%%i" ..\DeploymentPackage\APK\
cd..

echo.
echo.
echo ========================================================================================
echo Building MqttWindows...
cd MqttWindows
dotnet publish -r win-x64  -p:PublishSingleFile=true --self-contained true
copy bin\Release\net10.0-windows\win-x64\publish\* ..\DeploymentPackage
if not exist ..\DeploymentPackage\MqttWindows.dll.config copy App.config ..\DeploymentPackage\MqttWindows.dll.config
cd ..