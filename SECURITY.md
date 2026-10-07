# Security policy

Switch Please installs a system-wide keyboard hook and types corrections into other
applications, so a flaw in it can matter more than its size suggests. Reports are welcome.

## Reporting a vulnerability

Please report it privately through GitHub: **Security → Report a vulnerability** on this
repository, or [this direct link](https://github.com/marrakesh/switch-please/security/advisories/new).
Please do not open a public issue for it.

Once a fix is released, the advisory is published with credit to the reporter, unless they
would rather not be named.

## What counts

For example:

- typed text leaving the machine, or reaching the disk when the settings say it does not;
- the password-field or excluded-application guards failing in a way that lets text there be
  read or rewritten;
- a way for another program, or a crafted settings or log file, to make Switch Please run code
  or type input nobody asked for;
- a release file that does not match the source it claims to be built from, or its
  `SHA256SUMS.txt`.

## Supported versions

Only the latest release. Fixes are not backported; the
[releases page](https://github.com/marrakesh/switch-please/releases/latest), or the update
check if it is switched on, brings the fix.
