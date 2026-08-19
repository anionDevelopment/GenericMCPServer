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

A tool-definitions-file SHALL declare, per tool, its name, the script which implements it, what it does and the names of
its parameters. The server SHALL offer exactly those values to its clients.

#### Scenario: A client asks which tools exist

- **WHEN** a client asks the server which tools it offers
- **THEN** it receives, per declared tool, the declared name, the declared description and a schema which contains the
  declared parameters

#### Scenario: A client calls a tool

- **WHEN** a client calls a declared tool with values for its parameters
- **THEN** the script which the declaration names is run with those values, and what it writes to its standard-output is
  the result of the call

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
