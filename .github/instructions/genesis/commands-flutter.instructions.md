---
applyTo: "**/*.dart,**/pubspec.yaml"
---

# Flutter / Dart Commands

## Resolve dependencies

```sh
flutter pub get
```

## Analyze

```sh
flutter analyze
```

## Test

```sh
flutter test
```

## Run

```sh
flutter run
```

## Format

```sh
dart format .
```

## Adding the platform heads

If the native iOS and Android projects are not present, generate them in place with
`flutter create --platforms=android,ios .` (add `--org` to set the reverse-DNS prefix for the
application id / bundle identifier).
