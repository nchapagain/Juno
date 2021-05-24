# Building the Juno Guest Agent
The Juno Guest agent project is configured to build targeting both Windows and Linux operating systems. The following
documentation describes how that process works.

### Custom Build Target
Inside of the .csproj file there is a custom target that defines the individual ```dotnet publish``` commands that are used
to compile the Juno.GuestAgent project for the multiple operating systems. There are limitations in MSBuild that
prevent the targeting of multiple operating systems as part of a single project build.

``` xml
  <Target Name="BuildForMultipleOS">
    <PropertyGroup>
        <PublishOptions Condition="'$(WithRestore)' == 'false'">--no-restore</PublishOptions>
    </PropertyGroup>
      
    <Exec Command="rd /S /Q $(TargetDir)win-x64" ContinueOnError="true" />
    <Exec Command="rd /S /Q $(TargetDir)linux-x64" ContinueOnError="true" />
    <Exec Command="dotnet publish $(ProjectPath) --configuration $(Configuration) --runtime win-x64 -p:PublishDir=$(TargetDir)win-x64 $(PublishOptions)" />
    <Exec Command="dotnet publish $(ProjectPath) --configuration $(Configuration) --runtime linux-x64 -p:PublishDir=$(TargetDir)linux-x64 $(PublishOptions)" />
    <Exec Command="dotnet pack $(ProjectPath)" />
  </Target>
```

### Building the Guest Agent
To execute the custom build target 'BuildForMultipleOS', the user can call the ```dotnet build``` command passing in the
name of that specific target.

```
S:\source\one\crc-air\src\Juno> dotnet build ".\Juno.GuestAgent\Juno.GuestAgent.csproj" /t:BuildForMultipleOS
```

Additionally, there is a command file in the Juno solution directory ```build-agents.cmd``` that can be used as well. This command
is used in the PR + Official builds to create all Juno agent packages that must target multiple operating systems.

```
S:\source\one\crc-air\src\Juno> build-agents.cmd
```