FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/SubClear.Api.csproj apps/api/
RUN dotnet restore apps/api/SubClear.Api.csproj
COPY apps/api/ apps/api/
RUN dotnet publish apps/api/SubClear.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Database__Provider=Sqlite
ENV Database__ConnectionString=Data Source=/app/data/subclear.db
# Required in Production. Supply at run time, e.g. -e Jwt__Key=...
ENV Jwt__Issuer=SubClear
ENV Jwt__Audience=SubClear
ENV SubscriptionApi__ProductCode=SubClear
# Set SubscriptionApi__UseStub=true only for non-production smoke tests.
# Live billing: SubscriptionApi__BaseUrl and SubscriptionApi__ApiKey (never bake secrets into the image).
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENTRYPOINT ["dotnet", "SubClear.Api.dll"]
