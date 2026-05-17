set dotenv-load := true

platform := if os_family() == "windows" { "windows" } else { "unix" }
platform_justfile := justfile_directory() + "/Justfile." + platform

# List recipes for the current platform.
default:
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" --list

# Check required local tools and Bannerlord path configuration.
doctor:
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" doctor

# Build the solution. Defaults to Stable_Debug.
build configuration="Stable_Debug":
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" build "{{ configuration }}"

# Build all supported configurations.
build-all:
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" build-all

# Run the NUnit test project. Defaults to Stable_Debug.
test configuration="Stable_Debug":
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" test "{{ configuration }}"

# Run tests for all supported configurations.
test-all:
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" test-all

# Build a local module zip and NuGet packages. Defaults to Stable_Release.
package configuration="Stable_Release":
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" package "{{ configuration }}"

# Build, test, then package using platform-specific recipes.
all:
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" all

# Remove local artifacts and clean the solution. Defaults to Stable_Debug.
clean configuration="Stable_Debug":
    @just --justfile "{{ platform_justfile }}" --working-directory "{{ justfile_directory() }}" clean "{{ configuration }}"
