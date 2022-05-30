# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2022-05-30

### Added

- Public wallet LNURL page for sharing.

### Changed

- Distinguish original invoice amount and actual amount settled.
- Improve hold invoice handling.

### Fixed

- Allow specifying explicit amount for zero amount invoices.

## [1.2.1] - 2022-04-30

### Added

- Refresh transactions list on update.
- Log exceptions in background watcher.
- Handling for hold invoices.
- Autofocus input fields.

### Fixed

- Allow for empty description when creating invoices.
- Handle cancelled invoices in background watcher.

## [1.2.0] - 2022-04-01

### Added

- LNURL-Pay for receiving transactions.
- API for accessing, updating and deleting LNbank wallets.
- Export wallet transactions for accounting (CSV and JSON).

## [1.1.1] - 2022-03-09

### Added

- API for creating LNbank wallets.

### Changed

- Use store invoice expiry time.
- Soft delete wallets (only mark as deleted).

### Fixed

- Websocket connection to update transaction states.
- Handle crashes in background service.
- Fix redirects.

## [1.1.0] - 2022-02-21

### Added

- Toggle for attaching description to pay request when receiving.
- Allow for empty description when receiving.
- Customize description when sending.
- Prevent deletion of wallet with balance.

### Changed

- Proper redirects on homepage (create wallet if none exists).
- Separate wallet list and wallet details views.
- Common wallet header for all views.

### Fixed

- Fee handling: User pays routing fee and needs to have a fee reserve when sending.
- Prevent paying payment requests multiple times.

## [1.0.4] - 2022-02-10

### Added

- Support for private route hints: Will be enabled if the connected store has the required setting or if the toggle on the receive page is activated.

### Changed

- Lowercase page paths.
- Remove button icons, improve wallet view.

### Fixed

- Logo link on Share page.

## [1.0.3] - 2022-02-01

### Added

- Form validation

### Changed

- Improve create wallet for LN node connection case.

### Fixed

- Non-admins cannot send and receive when using the internal node.
