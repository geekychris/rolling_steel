# Rolling Steel - convenience wrappers around scripts/

.PHONY: build run verify shots setup clean

build:   ## build the macOS player
	@scripts/build.sh

run: build   ## build if needed, then play
	@scripts/run.sh

verify:  ## headless: assert all three courses are completable
	@scripts/verify.sh

shots:   ## capture in-engine screenshots into shots/
	@scripts/screenshots.sh

setup:   ## regenerate materials, scene and player settings
	@scripts/setup.sh

clean:   ## remove build output and generated caches
	rm -rf Builds .build-clone Logs shots Library Temp obj
