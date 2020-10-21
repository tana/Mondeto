.PHONY: shooting-game copy

shooting-game: raygun.wasm bullet.wasm target.wasm

WASM_FILES = test.wasm infinite_loop_test.wasm raygun.wasm bullet.wasm target.wasm
COPY_DEST = ../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts mondeto.ts

bullet.wasm: bullet.ts mondeto.ts

target.wasm: target.ts mondeto.ts