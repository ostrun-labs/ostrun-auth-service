# Security Policy

This service handles credentials, sessions and token signing, so security reports are welcome and taken seriously.

## Supported versions

The project is pre-1.0. Only the latest commit on `main` receives fixes.

## Reporting a vulnerability

Please do not open a public issue, pull request or discussion for a security problem.

Report it privately through GitHub instead: open the repository's **Security** tab and click **Report a vulnerability**. Include:

- what is affected (endpoint, component, configuration)
- steps to reproduce, or a proof of concept
- the impact you expect

You should get a first response within a week. Once a fix is ready, it is released and the advisory is published, crediting you unless you ask otherwise.

## Scope

In scope: the code in this repository, its Dockerfile, and the default configuration it ships with.

Out of scope: deployments that override the documented configuration (for example reusing a JWT signing key across instances, or trusting arbitrary origins), and vulnerabilities in third-party dependencies that are already publicly known. Please report those upstream.
