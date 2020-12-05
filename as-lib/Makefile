.PHONY: docs

FILES = assembly/index.ts assembly/wrapper.ts assembly/mondeto.ts

DOCS_DIR = docs
docs:
	npx typedoc --ignoreCompilerErrors --out $(DOCS_DIR) $(FILES)
