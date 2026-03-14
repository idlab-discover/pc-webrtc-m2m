#!/usr/bin/env python3

from __future__ import annotations

import argparse
import csv
import re
import struct
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Iterable


class ParseError(ValueError):
	pass


@dataclass(frozen=True)
class FieldDefinition:
	name: str
	type_byte: int
	value_size: int


@dataclass(frozen=True)
class MetricDefinition:
	name: str
	metric_id: int
	fields: tuple[FieldDefinition, ...]

	@property
	def row_size(self) -> int:
		return sum(field.value_size for field in self.fields)


@dataclass(frozen=True)
class ParsedRow:
	metric_id: int
	client_id: int | None
	timestamp: int
	values: tuple[object, ...]


SIGNED_NUMERIC = 1
UNSIGNED_NUMERIC = 2
SIGNED_FLOAT = 3
UNSIGNED_FLOAT = 4
STRING = 5
CUSTOM = 6


def parse_arguments() -> argparse.Namespace:
	parser = argparse.ArgumentParser(
		description=(
			"Parse a metrics header file and create one CSV file per metric. "
			"If a data file is provided, rows are also decoded into those CSV files."
		)
	)
	parser.add_argument("input_file", type=Path, help="Path to the metric definition binary file")
	parser.add_argument("output_dir", type=Path, help="Directory where CSV files should be written")
	parser.add_argument(
		"--data-file",
		type=Path,
		help=(
			"Optional path to the metric values binary file. This expects repeated "
			"[clientId:uint32][metricId:uint32][numValues:int32][rows...] blocks."
		),
	)
	parser.add_argument(
		"--timestamp-column",
		default=None,
		help="Optional CSV column name for the 8-byte row timestamp.",
	)
	parser.add_argument(
		"--client-column",
		default=None,
		help="Optional CSV column name for the metric client ID when --data-file is used.",
	)
	parser.add_argument(
		"--data-layout",
		choices=("auto", "global-client", "per-block-client"),
		default="auto",
		help=(
			"Layout used by --data-file. 'global-client' expects one client ID at the start of the file, "
			"then repeated metric blocks. 'per-block-client' expects a client ID before every metric block."
		),
	)
	return parser.parse_args()


def read_uint32(buffer: bytes, offset: int) -> tuple[int, int]:
	if offset + 4 > len(buffer):
		raise ParseError("Unexpected end of file while reading uint32")
	return struct.unpack_from("<I", buffer, offset)[0], offset + 4


def read_uint64(buffer: bytes, offset: int) -> tuple[int, int]:
	if offset + 8 > len(buffer):
		raise ParseError("Unexpected end of file while reading uint64")
	return struct.unpack_from("<Q", buffer, offset)[0], offset + 8


def read_int32(buffer: bytes, offset: int) -> tuple[int, int]:
	if offset + 4 > len(buffer):
		raise ParseError("Unexpected end of file while reading int32")
	return struct.unpack_from("<i", buffer, offset)[0], offset + 4


def read_bytes(buffer: bytes, offset: int, size: int) -> tuple[bytes, int]:
	if size < 0 or offset + size > len(buffer):
		raise ParseError("Unexpected end of file while reading bytes")
	return buffer[offset : offset + size], offset + size


def parse_field_buffer(header_buffer: bytes) -> tuple[FieldDefinition, ...]:
	composite_fields = try_parse_composite_fields(header_buffer)
	if composite_fields is not None:
		return composite_fields
	return parse_scalar_fields(header_buffer)


def try_parse_composite_fields(header_buffer: bytes) -> tuple[FieldDefinition, ...] | None:
	offset = 0
	fields: list[FieldDefinition] = []
	while offset < len(header_buffer):
		if offset + 9 > len(header_buffer):
			return None
		field_name_length, offset = read_uint32(header_buffer, offset)
		if field_name_length == 0:
			return None
		raw_field_name, offset = read_bytes(header_buffer, offset, field_name_length)
		if offset + 5 > len(header_buffer):
			return None
		type_byte = header_buffer[offset]
		offset += 1
		value_size, offset = read_uint32(header_buffer, offset)
		fields.append(
			FieldDefinition(
				name=raw_field_name.decode("utf-8", errors="replace"),
				type_byte=type_byte,
				value_size=value_size,
			)
		)
	return tuple(fields) if fields else None


def parse_scalar_fields(header_buffer: bytes) -> tuple[FieldDefinition, ...]:
	if len(header_buffer) % 5 != 0:
		raise ParseError("Scalar metric header length is not a multiple of 5 bytes")
	fields: list[FieldDefinition] = []
	offset = 0
	field_index = 0
	while offset < len(header_buffer):
		type_byte = header_buffer[offset]
		offset += 1
		value_size, offset = read_uint32(header_buffer, offset)
		field_index += 1
		field_name = "value" if field_index == 1 else f"value_{field_index}"
		fields.append(FieldDefinition(name=field_name, type_byte=type_byte, value_size=value_size))
	return tuple(fields)


def parse_metric_definitions(data: bytes) -> list[MetricDefinition]:
	metrics: list[MetricDefinition] = []
	offset = 0
	while offset < len(data):
		metric_name_length, offset = read_uint32(data, offset)
		if metric_name_length == 0:
			raise ParseError("Metric name length cannot be zero")
		raw_metric_name, offset = read_bytes(data, offset, metric_name_length)
		metric_id, offset = read_uint32(data, offset)
		header_length, offset = read_uint32(data, offset)
		if header_length == 0:
			raise ParseError("Header length cannot be zero")
		header_buffer, offset = read_bytes(data, offset, header_length)
		fields = parse_field_buffer(header_buffer)
		metrics.append(
			MetricDefinition(
				name=raw_metric_name.decode("utf-8", errors="replace"),
				metric_id=metric_id,
				fields=normalize_field_names(fields),
			)
		)
	return metrics


def normalize_field_names(fields: Iterable[FieldDefinition]) -> tuple[FieldDefinition, ...]:
	counts: dict[str, int] = {}
	normalized: list[FieldDefinition] = []
	for field in fields:
		current_count = counts.get(field.name, 0) + 1
		counts[field.name] = current_count
		if current_count == 1:
			normalized.append(field)
			continue
		normalized.append(
			FieldDefinition(
				name=f"{field.name}_{current_count}",
				type_byte=field.type_byte,
				value_size=field.value_size,
			)
		)
	return tuple(normalized)


def sanitize_filename(metric_name: str) -> str:
	sanitized = re.sub(r"[^A-Za-z0-9._-]+", "_", metric_name).strip("._")
	return sanitized or "metric"


def create_csv_files(
	output_dir: Path,
	metrics: Iterable[MetricDefinition],
	timestamp_column: str | None,
	client_column: str | None,
) -> dict[int, Path]:
	output_dir.mkdir(parents=True, exist_ok=True)
	used_paths: set[Path] = set()
	csv_paths: dict[int, Path] = {}
	for metric in metrics:
		filename = sanitize_filename(metric.name)
		csv_path = output_dir / f"{filename}.csv"
		if csv_path in used_paths:
			csv_path = output_dir / f"{filename}_{metric.metric_id}.csv"
		used_paths.add(csv_path)
		csv_paths[metric.metric_id] = csv_path
		with csv_path.open("w", newline="", encoding="utf-8") as handle:
			writer = csv.writer(handle)
			header_row: list[str] = []
			if client_column:
				header_row.append(client_column)
			if timestamp_column:
				header_row.append(timestamp_column)
			header_row.extend(field.name for field in metric.fields)
			writer.writerow(header_row)
	return csv_paths


def decode_metric_row(row_buffer: bytes, metric: MetricDefinition) -> list[object]:
	values: list[object] = []
	offset = 0
	for field in metric.fields:
		raw_value, offset = read_bytes(row_buffer, offset, field.value_size)
		values.append(decode_field_value(raw_value, field))
		#print(f"Decoded field {field.name} with raw value {raw_value.hex()} into {values[-1]}")
	return values


def decode_field_value(raw_value: bytes, field: FieldDefinition) -> object:
	if field.type_byte == SIGNED_NUMERIC:
		return decode_integer(raw_value, signed=True)
	if field.type_byte == UNSIGNED_NUMERIC:
		return decode_integer(raw_value, signed=False)
	if field.type_byte in {SIGNED_FLOAT, UNSIGNED_FLOAT}:
		if field.value_size == 4:
			return struct.unpack("<f", raw_value)[0]
		if field.value_size == 8:
			return struct.unpack("<d", raw_value)[0]
		raise ParseError(f"Unsupported floating point width: {field.value_size}")
	if field.type_byte in {STRING, CUSTOM}:
		return raw_value.split(b"\x00", 1)[0].decode("utf-8", errors="replace")
	return raw_value.hex()


def decode_integer(raw_value: bytes, signed: bool) -> int:
	format_map = {
		(1, False): "<B",
		(2, False): "<H",
		(4, False): "<I",
		(8, False): "<Q",
		(1, True): "<b",
		(2, True): "<h",
		(4, True): "<i",
		(8, True): "<q",
	}
	key = (len(raw_value), signed)
	if key not in format_map:
		raise ParseError(f"Unsupported integer width: {len(raw_value)}")
	return struct.unpack(format_map[key], raw_value)[0]


def append_metric_rows(
	data_file: Path,
	metrics_by_id: dict[int, MetricDefinition],
	csv_paths: dict[int, Path],
	timestamp_column: str | None,
	client_column: str | None,
	data_layout: str,
) -> None:
	data = data_file.read_bytes()
	rows = parse_metric_rows(data, metrics_by_id, data_layout)
	for row in rows:
		with csv_paths[row.metric_id].open("a", newline="", encoding="utf-8") as handle:
			writer = csv.writer(handle)
			output_row: list[object] = []
			if client_column and row.client_id is not None:
				output_row.append(row.client_id)
			if timestamp_column:
				output_row.append(row.timestamp)
			output_row.extend(row.values)
			writer.writerow(output_row)


def parse_metric_rows(
	data: bytes,
	metrics_by_id: dict[int, MetricDefinition],
	data_layout: str,
) -> tuple[ParsedRow, ...]:
	return parse_rows_per_block_client(data, metrics_by_id)



def parse_rows_per_block_client(data: bytes, metrics_by_id: dict[int, MetricDefinition]) -> tuple[ParsedRow, ...]:
	rows: list[ParsedRow] = []
	offset = 0
	while offset < len(data):
		client_id, offset = read_uint32(data, offset)
		data_size, offset = read_uint32(data, offset)
		current_size = 0
		while current_size < data_size:
			previous_offset = offset
			parsed_rows, offset = parse_metric_block(data, metrics_by_id, offset, client_id)
			current_size += offset-previous_offset
			rows.extend(parsed_rows)
	return tuple(rows)


def parse_metric_block(
	data: bytes,
	metrics_by_id: dict[int, MetricDefinition],
	offset: int,
	client_id: int | None,
) -> tuple[tuple[ParsedRow, ...], int]:
	metric_id, offset = read_uint32(data, offset)
	#print(f"Parsing metric block for metric ID {metrics_by_id[metric_id].name}:{metric_id} at offset {offset} (out of {len(data)})")
	num_values, offset = read_int32(data, offset)
	#print(f"Parsing metric block for metric ID {metrics_by_id[metric_id].name}:{metric_id} with {num_values} values at offset {offset} (out of {len(data)})")
	if num_values < 0:
		raise ParseError(f"Negative numValues for metricId {metrics_by_id[metric_id].name}:{metric_id} {num_values}")
	metric = metrics_by_id.get(metric_id)
	if metric is None:
		raise ParseError(f"No metric definition found for metricId {metric_id}")
	rows: list[ParsedRow] = []
	for _ in range(num_values):
		#print(num_values)
		timestamp, offset = read_uint64(data, offset)
		row_buffer, offset = read_bytes(data, offset, metric.row_size)
		rows.append(
			ParsedRow(
				metric_id=metric_id,
				client_id=client_id,
				timestamp=timestamp,
				values=tuple(decode_metric_row(row_buffer, metric)),
			)
		)
	return tuple(rows), offset


def main() -> None:
	args = parse_arguments()
	definitions_data = args.input_file.read_bytes()
	metric_definitions = parse_metric_definitions(definitions_data)
	csv_paths = create_csv_files(args.output_dir, metric_definitions, args.timestamp_column, args.client_column)

	if args.data_file:
		append_metric_rows(
			args.data_file,
			{metric.metric_id: metric for metric in metric_definitions},
			csv_paths,
			args.timestamp_column,
			args.client_column,
			args.data_layout,
		)

	print(f"Wrote {len(csv_paths)} CSV file(s) to {args.output_dir}")


if __name__ == "__main__":
	main()