# Code signing policy

> **Status:** an application to SignPath Foundation is under way. Until it is approved the
> releases stay unsigned, as they always have been, and this page describes how they will be
> signed. This note goes when the first signed release does.

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

## What gets signed

The executables and installers attached to a [release](https://github.com/marrakesh/switch-please/releases),
and nothing else:

- `SwitchPlease.exe`, `SwitchPlease-runtime-required.exe`, `SwitchPlease-arm64.exe` and
  `SwitchPlease-arm64-runtime-required.exe`;
- `SwitchPlease-Setup.exe` and `SwitchPlease-Setup-arm64.exe`, which install the signed
  executables above.

Every one of them is built by [the release workflow](../.github/workflows/release.yml) on a
GitHub-hosted runner, from the source in this repository, when a version tag is pushed.
SignPath checks that a signing request really comes from that workflow, and each request is
approved by hand before anything is signed. Nothing built on a personal machine is ever
submitted.

The self-contained builds include the .NET runtime, which Microsoft publishes under the MIT
licence. There are no other third-party components.

## Team

| Role | Members |
|---|---|
| Committers and reviewers | [Oleksii Ozerov](https://github.com/marrakesh) |
| Approvers | [Oleksii Ozerov](https://github.com/marrakesh) |

Everyone in these roles uses multi-factor authentication on GitHub and on SignPath.

## Privacy

This program will not transfer any information to other networked systems unless
specifically requested by the user or the person installing or operating it.

The one request it can make is the update check: it asks GitHub for the tag of the latest
release, sends nothing about the user, and is off until switched on. The
[README](../README.md#privacy) has the details, including what is written to disk.

## Setting it up

For a maintainer, once SignPath Foundation has accepted the project. The workflow skips every
signing step until the repository variable below exists, so nothing here has to be done in
any particular order.

1. In SignPath, the project's slug is `switch-please` and its signing policy's slug is
   `release-signing`. Link the predefined **GitHub.com** trusted build system to the project,
   and install the SignPath GitHub App on the repository.
2. Add two artifact configurations to the project. The workflow uploads each round of files
   as a zip with the files at its root.

   `executables`:

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <pe-file path="SwitchPlease.exe"><authenticode-sign/></pe-file>
       <pe-file path="SwitchPlease-runtime-required.exe"><authenticode-sign/></pe-file>
       <pe-file path="SwitchPlease-arm64.exe"><authenticode-sign/></pe-file>
       <pe-file path="SwitchPlease-arm64-runtime-required.exe"><authenticode-sign/></pe-file>
     </zip-file>
   </artifact-configuration>
   ```

   `installers`:

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <pe-file path="SwitchPlease-Setup.exe"><authenticode-sign/></pe-file>
       <pe-file path="SwitchPlease-Setup-arm64.exe"><authenticode-sign/></pe-file>
     </zip-file>
   </artifact-configuration>
   ```

   Each file is named rather than matched with a wildcard, so a file that stops being built
   fails the request instead of quietly going out unsigned.
3. In the repository's settings on GitHub, add the secret `SIGNPATH_API_TOKEN` (a token for a
   SignPath user who may submit to `release-signing`) and the variable
   `SIGNPATH_ORGANIZATION_ID`.
4. Push the next version tag. The run stops twice to wait for approval in SignPath: once for
   the executables, and once for the installers that are built from them.

Then remove the status note at the top of this page, and change the README's install section,
which still says the files are not signed.

The uninstaller Inno Setup writes at install time is not signed. Signing it needs the signing
tool to run during the installer build, which a remote signing service cannot do.
