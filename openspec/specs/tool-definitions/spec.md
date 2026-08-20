# tool-definitions Specification

## Purpose

Defines the property which makes this product generic: it offers tools without knowing any of them. The product provides
the means to declare a tool; which tools exist is decided by an administrator and never by the product itself.

This is not an implementation-detail but the reason the product exists. A tool which is built into the product would make
every user of the product carry it, would make its lifecycle depend on the release-cycle of the product and would turn the
product into an application for one purpose instead of a server for arbitrary ones.

## Requirements

### Requirement: The product contains no tool of its own

The source-code of this product SHALL NOT contain any tool. The set of tools the server offers SHALL be determined
exclusively by the tool-definitions-file the server is configured with.

#### Scenario: A new tool is needed

- **WHEN** a new tool is needed
- **THEN** it is created by adding a script and an entry to a tool-definitions-file, and no source-code of this repository
  is changed and nothing is recompiled

#### Scenario: A tool-name appears in the source-code

- **WHEN** the name of a tool, the name of one of its parameters or the name of the script which implements it appears
  anywhere in the source-code of a codeunit
- **THEN** this is a defect, which is resolved by moving that knowledge into a tool-definitions-file

#### Scenario: No tool-definitions-file is configured

- **WHEN** the server is started without a tool-definitions-file
- **THEN** it starts and offers no tool, because a tool exists only because somebody declared it

### Requirement: A declaration states everything a client needs to know about a tool

A tool-definitions-file SHALL declare, per tool, its name, its type, what implements it, what it does and the names of
its parameters. The server SHALL offer exactly those values to its clients.

#### Scenario: A client asks which tools exist

- **WHEN** a client asks the server which tools it offers
- **THEN** it receives, per declared tool, the declared name, the declared description and a schema which contains the
  declared parameters

#### Scenario: A client calls a tool

- **WHEN** a client calls a declared tool with values for its parameters
- **THEN** the script which the declaration names is run with those values, and what it writes to its standard-output is
  the result of the call

### Requirement: The type of a tool is declared and not guessed

Every declaration SHALL state the type of the tool, which is one of "python", "shell" and "web-request". The server
SHALL NOT derive the type from the name or the extension of a file.

#### Scenario: A tool is declared without a type or with an unknown one

- **WHEN** a declaration states no type or a type which does not exist
- **THEN** the server fails while starting and names the types which are possible

#### Scenario: A declaration states something which does not belong to its type

- **WHEN** a declaration states requirements for a tool which is no python-tool, allowed addresses for a tool which runs
  a script, or a script for a tool which requests an address
- **THEN** the server fails while starting instead of ignoring that value, because ignoring it would let an
  administrator believe in an effect which does not exist

### Requirement: A python-tool runs in a virtual environment of its own

The server SHALL run the script of a python-tool in a virtual environment which belongs to that tool alone and which
contains the packages the declaration states as its requirements.

#### Scenario: A python-tool is called for the first time

- **WHEN** a python-tool is called and no virtual environment exists for it
- **THEN** the environment is created, the declared requirements are installed into it and the script is run in it

#### Scenario: The requirements of a tool are changed

- **WHEN** a python-tool is called whose declared requirements differ from the ones its environment was built with
- **THEN** the environment is built again, so that a changed declaration can not be without effect

#### Scenario: An environment can not be built

- **WHEN** the creation of an environment or the installation of its requirements fails
- **THEN** the call fails with what the failing program reported and the incomplete environment is removed, so that it
  can not be mistaken for a working one

### Requirement: A tool which requests an address reaches only allowed addresses

A tool of the type "web-request" SHALL request an address only if a rule of its declaration allows it, and it SHALL
answer with the status-code and the body of that request.

#### Scenario: An address is requested which no rule allows

- **WHEN** a client asks such a tool for an address which no rule of the declaration matches
- **THEN** the call fails and no request is sent, because everything which is not allowed is denied

#### Scenario: An address only contains an allowed address

- **WHEN** a client asks for an address which contains an allowed address somewhere without being it
- **THEN** the call fails, because a rule has to match the whole address

#### Scenario: An address states a protocol

- **WHEN** a client asks for an address which states a protocol other than https
- **THEN** the call fails, because the protocol of such a tool is always https and is not part of an address

#### Scenario: An allowed address answers with a redirect

- **WHEN** an allowed address answers with a redirect
- **THEN** the redirect is handed to the client as the answer it is and is not followed, because the address it points
  to was never checked against the rules

### Requirement: A tool is reachable only for a client which is allowed to reach it

The server SHALL answer a call of a tool only when the request carries a valid access-token or the tool is declared as
one which everybody may use.

#### Scenario: A client calls a tool without a valid access-token

- **WHEN** a tool which is not declared as anonymous is called without a valid access-token
- **THEN** the request is answered with the status-code 401 and the tool is not run

#### Scenario: A client asks which tools exist without a valid access-token

- **WHEN** the list of the tools is asked for without a valid access-token
- **THEN** it contains exactly the tools which are declared as anonymous, so that a server which is reachable by
  accident does not tell what else it could do

#### Scenario: An administrator creates an access-token

- **WHEN** an administrator creates an access-token for a client
- **THEN** the token is shown once, only its hash is stored, it is valid up to a stated point in time and it is usable
  immediately without restarting the server

#### Scenario: A client needs a second access-token

- **WHEN** a client is given a second access-token while its first one is still valid
- **THEN** both tokens work, so that a token can be exchanged without an interruption

#### Scenario: An access-token expires

- **WHEN** the point in time up to which a token is valid has passed
- **THEN** the token is no access anymore

### Requirement: A tool receives exactly the credentials which are assigned to it

An access to a foreign service SHALL be configured at the server and assigned to a tool by its declaration. The server
SHALL hand a tool exactly the assigned accesses and SHALL NOT hand it any other.

#### Scenario: A tool needs an access

- **WHEN** a tool is called whose declaration assigns an access to it
- **THEN** the tool receives the value which the server is configured with, under the name the declaration gave it, and
  the client never provides it

#### Scenario: Another tool has an access

- **WHEN** a tool is called while another tool has an access assigned to it
- **THEN** the first tool can not read that access, so that a tool of one origin can not use the access of a tool of
  another origin

#### Scenario: An assigned access is not configured

- **WHEN** the server is started with a declaration which assigns an access which is not configured
- **THEN** the server fails while starting and names the access, so that a forgotten secret is visible while deploying

#### Scenario: A tool writes its access into its output

- **WHEN** the message of a failed tool-call contains the value of an access
- **THEN** the value is replaced before the message reaches the client

### Requirement: A broken declaration is detected when the server starts

The server SHALL reject a tool-definitions-file which is incomplete, ambiguous or unreadable, and it SHALL do so while
starting.

#### Scenario: A declaration is incomplete or ambiguous

- **WHEN** a tool-definitions-file declares a tool without a name, a tool without a script, a parameter without a name or
  the same tool-name more than once, or is not a valid json-document
- **THEN** the server fails while starting, so that the mistake is visible immediately instead of only when somebody
  happens to call the affected tool

#### Scenario: A configured tool-definitions-file does not exist

- **WHEN** a tool-definitions-file is configured but does not exist
- **THEN** the server fails while starting, because a configured-but-missing file is a mistake and not a declaration of
  zero tools

### Requirement: The values of the parameters can not be interpreted as anything but values

The server SHALL pass the values which a client provides to a script in a way in which they can not become a command, an
option or a path of their own.

#### Scenario: A value contains characters which a shell would interpret

- **WHEN** a client calls a tool with a value which contains characters a shell would interpret as a command-separator, a
  substitution or a redirection
- **THEN** the script receives that value unchanged as exactly one argument, and nothing of it is executed

#### Scenario: A value looks like an option

- **WHEN** a client calls a tool with a value which looks like a commandline-option
- **THEN** the script receives it as the value of the parameter it belongs to and not as an option

#### Scenario: A declaration names a script outside of its own folder

- **WHEN** a declaration names a script which does not lie inside the folder of the tool-definitions-file
- **THEN** the tool fails instead of starting that program

### Requirement: Whether a value is acceptable is decided by the script

The check whether a value has the format a tool expects SHALL be implemented in the script which implements that tool, and
the reason of a rejection SHALL reach the client.

#### Scenario: A value does not have the expected format

- **WHEN** a client calls a tool with a value which the script does not accept
- **THEN** the call is answered as a failed tool-call whose message contains what the script reported, so that the caller
  learns what was wrong and can correct it

#### Scenario: A format-check would be added to the server

- **WHEN** a check of the format of a value would be added to a codeunit of this repository
- **THEN** this is a defect, because the server does not know the tools and such a check would make it know one

### Requirement: A failed tool-call is visible in the answer and in its status-code

A tool-call which did not work SHALL be answered as a failed call of the protocol whose message states the reason, and
the http-answer which contains it SHALL carry the status-code 502.

#### Scenario: A tool fails

- **WHEN** a tool-call fails, no matter whether the script ended with an exit-code which is not 0, ran into its timeout,
  could not be prepared or requested an address which is not allowed
- **THEN** the client receives the message of the failure as the result of the call and the http-answer carries the
  status-code 502, because the failure comes from the program behind the server and the server is the gateway to it

#### Scenario: A tool works

- **WHEN** a tool-call works
- **THEN** the http-answer carries the status-code of a normal answer, so that the status-code says whether the call
  did what it should
