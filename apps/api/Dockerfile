FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY apps/api/SubClear.Api.csproj apps/api/
RUN dotnet restore apps/api/SubClear.Api.csproj
COPY apps/api/ apps/api/
RUN dotnet publish apps/api/SubClear.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Database__Provider=Sqlite
ENV Database__ConnectionString=Data Source=/app/data/subclear.db
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "SubClear.Api.dll"]
