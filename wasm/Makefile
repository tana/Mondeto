.PHONY: shooting-game copy docs

shooting-game: raygun.wasm bullet.wasm target.wasm

WASM_FILES = test.wasm infinite_loop_test.wasm raygun.wasm bullet.wasm target.wasm
COPY_DEST = ../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

DOCS_DIR = docs
docs:
	npx typedoc --ignoreCompilerErrors --out $(DOCS_DIR) mondeto.ts

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts mondeto.ts

bullet.wasm: bullet.ts mondeto.ts

target.wasm: target.ts mondeto.ts