# tool-definitions Specification

## Purpose

Defines the property which makes this product generic: it offers tools, resources and prompts without knowing any of
them. The product provides the means to declare an mcp-server with its tools, its resources, its prompts and the values
which the arguments of those prompts can have; which of them exist is decided by an administrator and never by the
product itself.

This is not an implementation-detail but the reason the product exists. A tool which was built into the product would
make every user of the product carry it, would make its lifecycle depend on the release-cycle of the product and would
turn the product into an application for one purpose instead of a server for arbitrary ones.

## Requirements

### Requirement: The product contains no tool of its own

The source-code of this product SHALL NOT contain any tool, any resource and any prompt. The set of tools, resources and
prompts which is offered SHALL be determined exclusively by the tool-definitions-file the server is configured with.

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
- **THEN** it starts and offers nothing, because a tool exists only because somebody declared it

#### Scenario: The resolved tool-definitions-file does not exist

- **WHEN** the server is started and the file its configuration points at (its persisted configuration-value
  `ToolDefinitionsFile`, which states the whole path of that file and is generated as the path of "Tools.json" in the
  tools-folder) does not exist there
- **THEN** it starts and offers nothing, exactly as if no tool-definitions-file was configured at all, because a
  freshly set up server which nobody pointed at a declaration yet is an expected state and not a mistake

#### Scenario: The folder of the configured tool-definitions-file does not exist

- **WHEN** the server is started and the folder which the configured tool-definitions-file lies in does not exist
- **THEN** the server fails while starting and states that folder, because a declaration which is missing is one
  nobody wrote yet while a folder which is missing is a path that names a place which is not there

#### Scenario: The declaration is handed over as a single file

- **WHEN** a tool-definitions-file is handed to the server as a single file in the tools-folder instead of as that
  whole folder (for a container: as a single mounted file)
- **THEN** the server uses exactly that file, without a folder of the host being handed over with it and without the
  rest of the tools-folder being covered by that mount

#### Scenario: The first configuration-file is generated

- **WHEN** the server starts for the first time and generates its configuration-file, and the initial path of the
  tool-definitions-file was stated from outside
- **THEN** that path is what the generated file states instead of the default, while every later start reads the path
  from that file alone - so stating it again afterwards changes nothing and the configuration-file stays the single
  place this is read from

### Requirement: What an administrator declares and what the server writes lie in two folders

The tool-definitions-file and the scripts it names SHALL lie in a tools-folder of the server, and the configuration-file
of the server together with the files of its clients, its roles and its access-tokens SHALL lie in a configuration-folder
which is a different folder. Handing the declaration over SHALL therefore not hand the state of the server over with it.

#### Scenario: An operator hands the declaration over

- **WHEN** an operator gives somebody access to the folder which holds the declaration and its scripts (for a container:
  mounts it as a volume)
- **THEN** that access covers no file the server wrote about itself, because the configuration-file and the files of the
  clients, the roles and the access-tokens lie in another folder

#### Scenario: The declaration is replaced

- **WHEN** the content of the tools-folder is replaced
- **THEN** the clients, the roles and the access-tokens of the server stay as they were, because none of them lies in
  that folder

### Requirement: One process hosts several mcp-servers which are separated from each other

A tool SHALL belong to exactly one declared mcp-server. Every mcp-server SHALL be reachable under an address of its own
which its declared url-namespace decides, and a client of one mcp-server SHALL NOT be able to see or reach the tools of
another one.

#### Scenario: Several mcp-servers are declared

- **WHEN** a tool-definitions-file declares more than one mcp-server
- **THEN** every one of them is reachable under "&lt;domain&gt;/&lt;url-namespace&gt;" and offers exactly its own tools

#### Scenario: A client asks one mcp-server which tools exist

- **WHEN** a client asks an mcp-server which tools it offers
- **THEN** it receives the tools of that mcp-server and no tool of another one

#### Scenario: The same tool-name is used in two mcp-servers

- **WHEN** two mcp-servers declare a tool of the same name
- **THEN** both are usable, because a tool-name is unambiguous within its mcp-server and not beyond it

#### Scenario: A url-namespace is declared twice

- **WHEN** two mcp-servers declare the same url-namespace
- **THEN** the server fails while starting, because it would not be decidable which of them an address means

### Requirement: A declaration states everything a client needs to know about a tool

A tool-definitions-file SHALL declare, per tool, its name, its type, what implements it, what it does and the names of
its parameters. The server SHALL offer exactly those values to its clients.

#### Scenario: A client asks which tools exist

- **WHEN** a client asks an mcp-server which tools it offers
- **THEN** it receives, per declared tool, the declared name, the declared description and a schema which contains the
  declared parameters

#### Scenario: A client calls a tool

- **WHEN** a client calls a declared tool with values for its parameters
- **THEN** what the declaration names is run with those values, and what it answers is the result of the call

### Requirement: A resource is declared like a tool and is read by its uri

An mcp-server SHALL offer the resources which its declaration states. Such a declaration SHALL state the uri the
resource is read by, its name, the type of its content and what implements it, which is one of the same types a tool is
implemented by. A resource SHALL take no parameter of the client, and the server SHALL hand the uri of the resource to
the script which implements it.

#### Scenario: A client asks which resources exist

- **WHEN** a client asks an mcp-server which resources it offers
- **THEN** it receives, per declared resource, the declared uri, the declared name, the declared description and the
  declared type of the content

#### Scenario: A client reads a resource

- **WHEN** a client reads a declared resource by its uri
- **THEN** what the declaration names is run, it receives the uri of that resource, and what it answers is the content
  of the resource together with the declared type of that content

#### Scenario: An mcp-server declares no resource

- **WHEN** an mcp-server declares no resource
- **THEN** it does not offer the resources of the protocol at all, so that a client of it does not ask for a list which
  is empty by definition

#### Scenario: A resource states parameters

- **WHEN** a declaration of a resource states parameters
- **THEN** the server fails while starting, because a resource is read by its uri and there is no value which a client
  could choose

#### Scenario: The same uri is declared twice within one mcp-server

- **WHEN** two resources of one mcp-server declare the same uri
- **THEN** the server fails while starting, because it would not be decidable which of them a client means

#### Scenario: A resource is requested from an address

- **WHEN** a resource of the type "web-request" is read
- **THEN** its own uri is the address which is requested, it states no rules for allowed addresses, and an answer whose
  status-code is no success is a failed read and not a content

#### Scenario: A read of a resource does not work

- **WHEN** a read of a resource does not work
- **THEN** it is answered as an error of the protocol whose message states the reason, and not as a content, because a
  client which asked for a document must not receive the reason of a failure as if it were that document

### Requirement: A prompt is declared like a tool and is asked for by its name

An mcp-server SHALL offer the prompts which its declaration states. Such a declaration SHALL state the name the prompt
is asked for by, its type, what implements it and the arguments it takes. A prompt SHALL be implemented by a script, and
the server SHALL hand the values which a client states for the declared arguments to that script in the same way in
which it hands the values of a tool to its script.

#### Scenario: A client asks which prompts exist

- **WHEN** a client asks an mcp-server which prompts it offers
- **THEN** it receives, per declared prompt, the declared name, the declared description and the declared arguments,
  each of them as a required one

#### Scenario: A client asks for a prompt

- **WHEN** a client asks for a declared prompt and states the values of its arguments
- **THEN** what the declaration names is run with those values, and what it wrote is the text of the prompt, handed over
  as one message of the role "user"

#### Scenario: An mcp-server declares no prompt

- **WHEN** an mcp-server declares no prompt
- **THEN** it does not offer the prompts of the protocol at all, so that a client of it does not ask for a list which is
  empty by definition

#### Scenario: A prompt is declared as one which requests an address

- **WHEN** a declaration of a prompt states the type "web-request"
- **THEN** the server fails while starting, because such a declaration takes the address it requests either from the
  client or from its own uri and a prompt has neither, so the type would have to invent one of the two for it

#### Scenario: The same prompt-name is declared twice within one mcp-server

- **WHEN** two prompts of one mcp-server declare the same name
- **THEN** the server fails while starting, because it would not be decidable which of them a client means

#### Scenario: A prompt could not be built

- **WHEN** the script of a prompt does not work
- **THEN** it is answered as an error of the protocol whose message states the reason, and not as a message of the
  prompt, because a client which asked for a prompt must not hand the reason of a failure to its model as if it were
  that prompt

### Requirement: The values which an argument of a prompt can have are declared with that argument

A declaration of a prompt MAY state, per argument, what provides the values which that argument can have. Such a
declaration SHALL be stated under the name of the argument it belongs to, and that name SHALL be one of the arguments
which the prompt declares. The server SHALL hand the script which provides those values the argument it is asked about
and the value which the client entered so far, in the same way in which it hands the values of a tool to its script,
and SHALL offer what that script wrote as the possible values of that argument.

#### Scenario: A client asks which values an argument can have

- **WHEN** a client asks which values an argument of a declared prompt can have and states the value which was entered
  so far
- **THEN** what the declaration names is run, it receives that argument and that value, and every non-empty line it
  wrote is one of the values the client receives

#### Scenario: An argument declares no values

- **WHEN** a client asks which values an argument can have which declares no such provider, or asks about something
  which is no prompt of that mcp-server
- **THEN** it is answered with no value and not with a failure, because "which values are possible" is a question a
  client may ask about anything and "none are known here" is an answer to it

#### Scenario: There are more values than one answer carries

- **WHEN** the script writes more values than the protocol allows one answer to carry
- **THEN** the client receives as many of them as the protocol allows together with how many there are in total, so
  that it can tell a complete list from a cut off one

#### Scenario: An mcp-server declares no such provider

- **WHEN** no prompt of an mcp-server declares the values of one of its arguments
- **THEN** that mcp-server does not offer the completions of the protocol at all, so that a client of it does not ask
  for values which are empty by definition

#### Scenario: Values are declared for an argument which the prompt does not take

- **WHEN** such a declaration stands under a name which is none of the arguments of its prompt
- **THEN** the server fails while starting, because it would describe a script which nobody ever calls

#### Scenario: Such a declaration states the type "web-request", parameters or a description

- **WHEN** such a declaration states the type "web-request", states parameters or states a description
- **THEN** the server fails while starting: it is asked which values an argument can have, so it has no address which a
  client could ask for, which values its script receives is decided by the mechanism, and a client asks about an
  argument it already chose and never chooses such a declaration itself

#### Scenario: Such a script does not work

- **WHEN** the script which provides the values does not work
- **THEN** it is answered as an error of the protocol whose message states the reason, and not as a value, because a
  client which asked which values are possible must not receive the reason of a failure as if it were one of them

### Requirement: A tool may declare the shape of its answer

A declaration of a tool MAY state the shape of its answer as a json-schema. The server SHALL offer that schema to the
clients of that tool, SHALL check the answer of the tool against it and SHALL hand the answer over as the structured
content of the result next to the text of the answer. A tool which states no such schema SHALL answer with text alone.

#### Scenario: A client asks which tools exist

- **WHEN** a client asks an mcp-server which tools it offers
- **THEN** it receives the declared schema with every tool which declared one, and no schema with a tool which
  declared none

#### Scenario: A tool which declared a schema answers

- **WHEN** a client calls such a tool and what the tool wrote fits the declared schema
- **THEN** the result carries that document as its structured content and, next to it, what the tool wrote as text, so
  that a client which reads no structured content receives what it received before

#### Scenario: An answer does not fit the declared schema

- **WHEN** what the tool wrote is no json-document or does not fit the declared schema
- **THEN** the call fails with a message which states what did not fit, because a client which was promised data of a
  known shape must not receive something else under that promise

#### Scenario: A declared schema is not usable

- **WHEN** a declaration states a schema which is no json-object or which is no usable json-schema
- **THEN** the server fails while starting, because a schema which checks nothing would let every answer pass

#### Scenario: Something which is no tool declares such a schema

- **WHEN** a resource, a prompt or a completion states such a schema
- **THEN** the server fails while starting: a resource states the type of its content, a prompt writes the text of the
  prompt and a completion writes one value per line, so none of them answers with something whose shape a client reads
  as data

### Requirement: A tool-call which its client cancelled is stopped

A client SHALL be able to stop a tool-call which is still running by cancelling the request it sent that call with. The
server SHALL stop the program behind that call and SHALL NOT require anything of the declaration or of the script for
it. Such a cancellation SHALL only be able to stop a call of the same mcp-server and of the same client.

#### Scenario: A client cancels a call which is running

- **WHEN** a client cancels the request of a tool-call which is still running
- **THEN** the program behind that call is stopped - the script together with everything it started, respectively the
  request which is waiting for a foreign service - and the call is answered with that reason

#### Scenario: The script started a program which started another one

- **WHEN** the script of a cancelled call had started a program which itself had started another one, and one of them
  has already ended by the time the call is cancelled
- **THEN** every program of that call is stopped nevertheless, because what is ended is the whole tree and not a chain
  which is walked from one living program to the next - a program which ended on its own must not be able to hide the
  programs below it

#### Scenario: A declared tool is cancellable without stating anything for it

- **WHEN** a tool is declared
- **THEN** a call of it is cancellable without the declaration stating anything for it: the tool takes no additional
  parameter, it sends nothing back and it does not even learn that it is cancellable

#### Scenario: A cancellation names a call of another client or of another mcp-server

- **WHEN** a cancellation states the id of a request which another client sent, or reaches another mcp-server than the
  call it names
- **THEN** nothing is stopped, because the id of a request is only unambiguous within the client which chose it, so
  which mcp-server and which client are meant is taken from the request the cancellation arrived with and not from the
  cancellation itself

#### Scenario: A cancellation names a call which is not running

- **WHEN** a cancellation names a call which is not running
- **THEN** nothing is stopped and nothing fails, because a client cancels a call it has no answer for yet, so a
  cancellation which arrives after the call ended is the normal case of one which arrives too late

#### Scenario: A cancelled call is no failed call

- **WHEN** a call was stopped because its client cancelled it
- **THEN** it is answered with that reason and with the status-code of a normal answer and not with the 502 of a failed
  tool-call, because the 502 states that the program behind this server failed and nothing failed here

#### Scenario: A cancellation names something which is no tool-call

- **WHEN** a cancellation names a request which was a read of a resource, a prompt or a completion
- **THEN** nothing is stopped, because such a request is answered in one go and a cancellation names the id of a
  request and not what that request was about

#### Scenario: A call was stopped

- **WHEN** a call was stopped because its client cancelled it
- **THEN** that is written to the log of the server, because a cancellation is answered with nothing at all and would
  otherwise leave no trace for the operator

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

### Requirement: A broken declaration is detected when the server starts

The server SHALL reject a tool-definitions-file which is incomplete, ambiguous or unreadable, and it SHALL do so while
starting.

#### Scenario: A declaration is incomplete or ambiguous

- **WHEN** a tool-definitions-file declares an mcp-server without a url-namespace, a tool without a name, a tool without
  a type, a parameter without a name, the same tool-name more than once within one mcp-server or a prompt which states
  no name, no type or no script, or is not a valid json-document
- **THEN** the server fails while starting, so that the mistake is visible immediately instead of only when somebody
  happens to call the affected tool

#### Scenario: An mcp-server could be used by nobody

- **WHEN** an mcp-server is declared which is not anonymous and which states no role which may use it
- **THEN** the server fails while starting, because such an mcp-server would exist and answer nothing

### Requirement: An mcp-server is reachable only for a client which is allowed to reach it

The server SHALL answer a request of an mcp-server only when that mcp-server is declared as anonymous or when the
request carries a valid access-token of a client which is in one of the roles the mcp-server declares.

#### Scenario: A client without a valid access-token

- **WHEN** a request reaches an mcp-server which is not anonymous without a valid access-token
- **THEN** it is answered with the status-code 401 and nothing of that mcp-server is handed out

#### Scenario: A client without a fitting role

- **WHEN** a request carries a valid access-token of a client which is in none of the roles the mcp-server declares
- **THEN** it is answered with the status-code 401 as well, so that a caller does not learn whether its token or its
  role was the reason

#### Scenario: An mcp-server is declared as anonymous

- **WHEN** an mcp-server is declared as one which everybody may use
- **THEN** it is reachable without an access-token, while the other mcp-servers of the same process stay behind theirs

#### Scenario: An administrator creates an access-token

- **WHEN** an administrator creates an access-token for a client
- **THEN** the token is shown once, only its hash is stored, it is valid up to a stated point in time and it is usable
  immediately without restarting the server

#### Scenario: A client needs a second access-token

- **WHEN** a client is given a second access-token while its first one is still valid
- **THEN** both tokens work, so that a token can be exchanged without an interruption

#### Scenario: A role is given or taken away

- **WHEN** a client is put into a role or removed from one
- **THEN** that takes effect at the next request, without restarting the server, because the roles are read per request

#### Scenario: A single client would be named in a declaration

- **WHEN** a declaration would name a single client or a single access-token
- **THEN** this is a defect: a declaration states which roles may use an mcp-server, and who is in a role is stated
  where the clients are administrated

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

### Requirement: What a tool does is the responsibility of the administrator

The product SHALL run what a declaration names and SHALL NOT claim to check what it does.

#### Scenario: A tool does something harmful

- **WHEN** a declared tool does something harmful on the server
- **THEN** this is the responsibility of whoever declared it, because a declared tool is a program on the server which
  runs with the rights of the server; the documentation of the product states this explicitly
