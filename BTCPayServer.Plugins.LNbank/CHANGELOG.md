# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2022-02-21

### Added

- Toggle for attaching description to pay request when receiving.
- Allow for empty description when receiving.
- Customize description when sending.
- Prevent deletion of wallet with balance.

### Changed

- Proper redirects on homepage (create wallet if none exists, go to wallet if there is only one).
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

- Logo link on Share page

## [1.0.3] - 2022-02-01

### Added

- Form validation

### Changed

- Improve create wallet for LN node connection case

### Fixed

- Non-admins cannot send and receive when using the internal node.