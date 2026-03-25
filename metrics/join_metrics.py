import pandas as pd
import numpy as np

# Load all source CSVs
qac = pd.read_csv("test/QualityAdaptationChoices.csv")
rtr = pd.read_csv("test/MDCReadyToRender.csv")
enc = pd.read_csv("test/MDCEncodingDone.csv")
dec = pd.read_csv("test/MDCDecodingDone.csv")
rfr = pd.read_csv("test/MDCRemoteFrameReceived.csv")

# --- Pre-compute per-frame aggregates ---

# Encoding latency per frame: max(encodingDoneTimestamp - capturingTimestamp) across descriptions
enc["encoding_latency"] = enc["encodingDoneTimestamp"] - enc["capturingTimestamp"]
enc_per_frame = enc.groupby("frameNr").agg(
    capturingTimestamp=("capturingTimestamp", "first"),
    max_encoding_latency=("encoding_latency", "max"),
).reset_index()

# Decoding latency per description: decodingDoneTimestamp - receivingTimestamp
# Join dec with rfr on (clientID, frameNr, descriptionID) to get receivingTimestamp
dec_joined = dec.merge(
    rfr[["clientID", "frameNr", "descriptionID", "receivingTimestamp"]],
    on=["clientID", "frameNr", "descriptionID"],
    how="left",
)
dec_joined["decoding_latency"] = dec_joined["decodingDoneTimestamp"] - dec_joined["receivingTimestamp"]

# Load late-received records and exclude those (clientID, frameNr, descriptionID) combos from dec_joined
# when computing max_decoding_done_ts (they arrived too late to be used for render timing)
late = pd.read_csv("test/MDCLateReceived.csv")[["clientID", "frameNr", "descriptionID"]]
late["_late"] = True
dec_joined_flagged = dec_joined.merge(late, on=["clientID", "frameNr", "descriptionID"], how="left")

dec_per_frame = dec_joined_flagged.groupby(["clientID", "frameNr"]).agg(
    # Both metrics only consider descriptions NOT in MDCLateReceived
    max_decoding_latency=("decoding_latency", lambda s: s[dec_joined_flagged.loc[s.index, "_late"].isna()].max()),
    max_decoding_done_ts=("decodingDoneTimestamp", lambda s: s[dec_joined_flagged.loc[s.index, "_late"].isna()].max()),
).reset_index()

# Delay till render: readyToRenderTimestamp - max(decodingDoneTimestamp) per (clientID, frameNr)
delay_df = rtr[["clientID", "frameNr", "readyToRenderTimestamp"]].merge(
    dec_per_frame[["clientID", "frameNr", "max_decoding_done_ts"]],
    on=["clientID", "frameNr"],
    how="left",
)
delay_df["delay_till_render"] = delay_df["readyToRenderTimestamp"] - delay_df["max_decoding_done_ts"]

# Overall latency per (clientID, frameNr): readyToRenderTimestamp - capturingTimestamp
latency_df = rtr.merge(enc_per_frame[["frameNr", "capturingTimestamp"]], on="frameNr", how="left")
latency_df["overall_latency"] = latency_df["readyToRenderTimestamp"] - latency_df["capturingTimestamp"]

# --- Build output rows ---

output_rows = []

for i, row in qac.iterrows():
    client_id = int(row["ClientID"])
    frame_start = int(row["FrameNr"])
    frame_end = int(qac.iloc[i + 1]["FrameNr"]) if i + 1 < len(qac) else None

    # Filter helpers
    def in_window(series):
        if frame_end is not None:
            return (series >= frame_start) & (series < frame_end)
        return series >= frame_start

    # --- Quality ---
    rtr_window = rtr[in_window(rtr["frameNr"]) & (rtr["clientID"] == client_id)]

    # Determine all frame numbers in window
    if frame_end is not None:
        all_frames = set(range(frame_start, frame_end))
    else:
        all_frames = set(rtr_window["frameNr"].tolist())

    rendered_frames = set(rtr_window["frameNr"].tolist())
    missing_count = len(all_frames - rendered_frames)

    qualities = list(rtr_window["qualityLevel"].tolist()) + [0] * missing_count
    quality_avg = np.mean(qualities) if qualities else 0.0
    quality_std = np.std(qualities, ddof=0) if len(qualities) > 1 else 0.0

    # --- Overall latency ---
    lat_window = latency_df[in_window(latency_df["frameNr"]) & (latency_df["clientID"] == client_id)]
    latency_avg = lat_window["overall_latency"].mean() if len(lat_window) > 0 else np.nan
    latency_std = lat_window["overall_latency"].std(ddof=0) if len(lat_window) > 1 else 0.0

    # --- Encoding latency ---
    enc_window = enc_per_frame[in_window(enc_per_frame["frameNr"])]
    enc_lat_avg = enc_window["max_encoding_latency"].mean() if len(enc_window) > 0 else np.nan
    enc_lat_std = enc_window["max_encoding_latency"].std(ddof=0) if len(enc_window) > 1 else 0.0

    # --- Decoding latency ---
    dec_window = dec_per_frame[in_window(dec_per_frame["frameNr"]) & (dec_per_frame["clientID"] == client_id)]
    dec_lat_avg = dec_window["max_decoding_latency"].mean() if len(dec_window) > 0 else np.nan
    dec_lat_std = dec_window["max_decoding_latency"].std(ddof=0) if len(dec_window) > 1 else 0.0

    # --- Delay till render ---
    dtr_window = delay_df[in_window(delay_df["frameNr"]) & (delay_df["clientID"] == client_id)]
    dtr_avg = dtr_window["delay_till_render"].mean() if len(dtr_window) > 0 else np.nan
    dtr_std = dtr_window["delay_till_render"].std(ddof=0) if len(dtr_window) > 1 else 0.0

    out = row.to_dict()
    out["quality_avg"] = round(quality_avg, 4)
    out["quality_std"] = round(quality_std, 4)
    out["latency_avg"] = round(latency_avg, 4) if not np.isnan(latency_avg) else np.nan
    out["latency_std"] = round(latency_std, 4)
    out["encoding_latency_avg"] = round(enc_lat_avg, 4) if not np.isnan(enc_lat_avg) else np.nan
    out["encoding_latency_std"] = round(enc_lat_std, 4)
    out["decoding_latency_avg"] = round(dec_lat_avg, 4) if not np.isnan(dec_lat_avg) else np.nan
    out["decoding_latency_std"] = round(dec_lat_std, 4)
    out["delay_till_render_avg"] = round(dtr_avg, 4) if not np.isnan(dtr_avg) else np.nan
    out["delay_till_render_std"] = round(dtr_std, 4)
    output_rows.append(out)

result = pd.DataFrame(output_rows)
result.to_csv("test/QualityAdaptationChoices_enriched.csv", index=False)
print(f"Done. Wrote {len(result)} rows to test/QualityAdaptationChoices_enriched.csv")
