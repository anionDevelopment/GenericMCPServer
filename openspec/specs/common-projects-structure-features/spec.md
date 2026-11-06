# common-projects-structure-features Specification

## Purpose

Every product maintains a single list of the features which a user can expect from it.
This list is the one place which answers, from the perspective of a user and not of the source-code, what the product can do and what it cannot do yet.

A feature-list is only useful if it is complete and if it is honest: it has to contain every feature which a user can observe, including the features which are planned but not implemented yet, and the stated availability of every feature has to be the one which the source-code of the current branch actually provides.
A list which silently drops a planned feature stops being an overview of what is planned, and a list whose availability does not follow the source-code misleads the reader about what the product can do.

This specification is product-agnostic: it defines the location, the structure and the meaning of the feature-list and the rules which keep it complete and honest, without naming any concrete feature or product.
It can be used unchanged in any product which follows the common project structure.

## Requirements

### Requirement: The product has a feature-list in a fixed place

The product SHALL contain a section `## Features` in `Other/Reference/Reference.md` which holds exactly one table that is the complete list of the features which a user can expect from the product.
Other documents MAY give a shorter overview of features, but this table SHALL be the one which is complete and which the other overviews agree with.

#### Scenario: The complete feature-list is requested

- **WHEN** the complete set of features of the product is to be looked up
- **THEN** it is found in the table of the section `## Features` in `Other/Reference/Reference.md`

#### Scenario: Another document lists features as well

- **WHEN** another document (for example `ReadMe.md`) gives an overview of features
- **THEN** that overview agrees with the table in `Other/Reference/Reference.md`, which stays the complete list

#### Scenario: The section does not exist yet

- **WHEN** a product has no section `## Features` in `Other/Reference/Reference.md`
- **THEN** the section and its table are created in the form which this specification defines

### Requirement: The feature-table has fixed columns

The table MUST have exactly the following columns in this order, with exactly these column-names:

- `Id`: the id of the feature.
- `State`: the availability of the feature.
- `Description`: what the feature is, described from the perspective of a user.

No further column SHALL be added to the table.

#### Scenario: The table is written

- **WHEN** the feature-table is created or a row is added
- **THEN** the table has exactly the columns `Id`, `State` and `Description` in this order and no other column

#### Scenario: Further information exists for a feature

- **WHEN** there is further information about a feature which does not fit into `Id`, `State` or `Description`
- **THEN** it is not added as a further column but is placed where it belongs (for example in the technical documentation),
  because the feature-table keeps exactly these three columns

### Requirement: The id of a feature has a fixed form

Every feature MUST have an id of the form `feat-<ascending number>`.
The number MUST be written without leading zeros (for example `feat-1` or `feat-12`, but not `feat-01`) and MUST be ascending.
An id MUST never be reused for another feature, not even after a feature was removed.

#### Scenario: A new feature is added

- **WHEN** a new feature is added to the table
- **THEN** its id is `feat-` followed by the highest number already used plus one

#### Scenario: A feature is removed

- **WHEN** a feature is removed from the table
- **THEN** its id is not given to any other feature afterwards

### Requirement: The state of a feature has a fixed set of values

The column `State` of every feature MUST have exactly one of the following values and no other value:

- `available`: a user can use the feature in the current state of the product.
- `unavailable`: a user cannot use the feature yet in the current state of the product.

#### Scenario: A state outside the set is written

- **WHEN** a value other than `available` or `unavailable` would describe the availability of a feature
- **THEN** it is not used; the feature is `available` if and only if a user can use it, and `unavailable` otherwise

### Requirement: The feature-list contains every user-facing feature

Every feature which a user can observe or expect from the product SHALL have a row in the table, judged from the perspective of a user and not from the structure of the source-code.
A feature which is planned but not implemented yet SHALL stay in the list with the state `unavailable` instead of being left out, so that the list is also an overview of what is planned.

#### Scenario: A feature exists but is not listed

- **WHEN** the product provides a feature which a user can use and which the table does not contain
- **THEN** the feature is added as a row with the state `available`

#### Scenario: A feature is planned

- **WHEN** a feature is planned for the product but a user cannot use it yet
- **THEN** it is listed as a row with the state `unavailable` and is not left out

#### Scenario: An entry is not a user-facing feature

- **WHEN** a row describes an internal detail which a user cannot observe instead of a feature which a user can expect
- **THEN** it does not belong into this table and is removed or moved to the technical documentation

### Requirement: The state of a feature reflects the current state of the source-code

The `State` of every feature SHALL be the one which the source-code of the current branch implements, determined by checking the source-code itself and not the documentation or comments.
A feature is `available` if and only if a user can actually use it in the current state of the source-code; otherwise it is `unavailable`.
A feature whose availability changed SHALL have its `State` corrected, and it SHALL NOT be removed from the table because of such a change.

#### Scenario: A feature became usable

- **WHEN** the source-code now lets a user use a feature which was listed as `unavailable`
- **THEN** the state of its row is set to `available`

#### Scenario: A feature became unusable

- **WHEN** the source-code does not let a user use a feature anymore which was listed as `available`
- **THEN** the state of its row is set to `unavailable` and the row stays in the table

#### Scenario: The feature-list is checked against the source-code

- **WHEN** it is checked whether the documentation of the product agrees with the source-code
- **THEN** the state of every feature in the table is checked against the source-code and every deviating state is corrected in
  the same run


<!-- This file is generated. Do not modify. -->