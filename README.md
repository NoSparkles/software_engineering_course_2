# software_engineering_course_2

## Commands to start tests:
```
dotnet test backend.Tests --collect:"XPlat Code Coverage" --settings coverlet.runsettings
reportgenerator -reports:"backend.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
start coverage-report/index.html
```