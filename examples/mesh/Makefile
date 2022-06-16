.PHONY: all copy clean

all: surface.wasm

WASM_FILES = surface.wasm
COPY_DEST = ../../Assets/StreamingAssets/wasm/
copy:
	cp $(WASM_FILES) $(COPY_DEST)

clean:
	rm $(WASM_FILES)

ASFLAGS = --disable bulk-memory
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

surface.wasm: surface.ts
