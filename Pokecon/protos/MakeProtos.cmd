@echo off
rem Set PROTOC=protoc.exe
Set PROTOC=../../packages/Google.Protobuf.Tools.3.0.0-beta3/tools/windows_x64/protoc.exe
"%PROTOC%" -I=. --csharp_out=. pokemon.proto
rem "%PROTOC%" -I=. --csharp_out=. pokemon.new.proto