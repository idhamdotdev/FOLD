@rem Gradle wrapper for Windows
@rem Copyright 2015 the original author or authors. Apache License 2.0.

@if "%DEBUG%"=="" @echo off
@rem Set local scope for the variables with windows NT shell
if "%OS%"=="Windows_NT" setlocal

set DIRNAME=%~dp0
if "%DIRNAME%"=="" set DIRNAME=.
set APP_BASE_NAME=%~n0
set APP_HOME=%DIRNAME%

@rem Resolve JAVA_HOME / java command
set JAVACMD=java
if defined JAVA_HOME (
    if exist "%JAVA_HOME%/bin/java.exe" (
        set "JAVACMD=%JAVA_HOME%/bin/java.exe"
    )
)

set CLASSPATH=%APP_HOME%\gradle\wrapper\gradle-wrapper.jar

@rem Execute Gradle
"%JAVACMD%" -Xmx64m -Xms64m -classpath "%CLASSPATH%" org.gradle.wrapper.GradleWrapperMain %*

:end
if "%OS%"=="Windows_NT" endlocal
