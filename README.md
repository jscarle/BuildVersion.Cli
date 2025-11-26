# Build Version Command Line Interface

A command line tool to emit automatic build versions in a CI/CD pipeline.

## Requirements

You must have the .NET 8, .NET 9, or .NET 10 runtime or SDK installed.

## Installation

The tool can be installed globally with the command: `dotnet tool install BuildVersion.Cli --global`

This will download, install, and alias the tool with the command `build-version`.

## Usage

Running `build-version --help` will display help as follows:
```
Description:
  A command line tool to emit automatic build versions in a CI/CD pipeline.

Usage:
  build-version [options]

Options:
  --environment <Development|Production|Staging> (REQUIRED)  Environment to generate the build version for.
  --output <DevOps|GitHub|Plain>                             The output format of the build version. [default: Plain]
  --base <base>                                              Override the automatically generated version using the base version
  --major <major>                                            Override the automatically generated major version
  --minor <minor>                                            Override the automatically generated minor version
  --patch <patch>                                            Override the patch version or use 'auto' to set it to the current week of the year
  --build <build>                                            Override the automatically generated build version
  --version                                                  Show version information
  -?, -h, --help                                             Show help and usage information
```
