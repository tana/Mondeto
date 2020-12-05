.PHONY: all shooting-game copy docs clean

all: test.wasm shooting-game

shooting-game: raygun.wasm bullet.wasm target.wasm

AS_WASM_FILES = test.wasm raygun.wasm bullet.wasm target.wasm
WASM_FILES = infinite_loop_test.wasm $(AS_WASM_FILES)
COPY_DEST = ../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

clean:
	rm $(AS_WASM_FILES)

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts

bullet.wasm: bullet.ts

target.wasm: target.ts

test.wasm: test.ts