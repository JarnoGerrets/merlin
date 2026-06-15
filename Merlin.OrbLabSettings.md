# Merlin OrbLab Settings

Exported: `2026-06-15T10:11:08`

Values marked with `requires_rebuild` rebuild the generated orb when applied.

## Generation

- `generation_seed` = `26062026` requires_rebuild

## Glow

- `cluster_halo_intensity` = `3.6000`
- `cluster_halo_radius_scale` = `2.0500`
- `sharp_geometry_alpha_scale` = `2.0000`
- `sharp_hot_mix_scale` = `0.8000`

## Orb Builder

- `natural_halo_dust_weight` = `1.0000`
- `natural_halo_connection_weight` = `1.0000`
- `natural_halo_route_weight` = `1.1500`
- `natural_halo_brightness_weight` = `1.0000`
- `natural_halo_min_score` = `0.1600`

## Center

- `core_glow_alpha_scale` = `0.4200`
- `core_glow_radius_scale` = `1.0000`
- `core_particle_brightness_scale` = `1.0000` requires_rebuild
- `core_particle_size_scale` = `1.0000` requires_rebuild
- `center_visual_size` = `0.2000` requires_rebuild
- `core_cluster_particle_count` = `1580.0000` requires_rebuild
- `core_radius_factor` = `0.9500` requires_rebuild

## Motion

- `orb_shell_deformation_enabled` = `on`
- `orb_shell_deformation_strength` = `0.3400`
- `orb_shell_deformation_speed` = `2.1500`
- `orb_shell_deformation_radius` = `1.0500`
- `orb_shell_deformation_alpha` = `0.2200`
- `cached_speech_motion_enabled` = `on`
- `real_speech_motion_enabled` = `off`
- `real_speech_motion_strength` = `0.1800`
- `real_speech_motion_speed` = `3.2000`
- `real_speech_motion_smoothing` = `4.8000`
- `real_speech_motion_region_blend` = `0.3400`
- `energy_pulse_min_speed` = `0.3400`
- `energy_pulse_max_speed` = `1.1200`
- `energy_pulse_speed_wobble` = `0.1000`

## Distribution

- `structural_core_fraction` = `0.2400` requires_rebuild
- `structural_mid_fraction` = `0.2200` requires_rebuild
- `structural_shell_probability` = `0.5600` requires_rebuild
- `structural_fill_radius_scale` = `0.9400` requires_rebuild
- `ambient_dust_inner_radius` = `0.3500` requires_rebuild
- `ambient_dust_outer_radius` = `1.1800` requires_rebuild

## Thinking

- `cluster_activation_base_lift` = `0.3400`
- `cluster_activation_bloom_boost` = `0.4600`
- `cluster_activation_cooldown_seconds` = `3.0000`
- `thinking_bloom_dampening` = `1.1500`
- `cluster_activation_strength_scale` = `1.0000`
- `cluster_activation_attack_min` = `0.1200`
- `cluster_activation_attack_max` = `0.1800`
- `cluster_activation_hold_min` = `1.0000`
- `cluster_activation_hold_max` = `1.1800`
- `cluster_activation_fade_min` = `1.0500`
- `cluster_activation_fade_max` = `1.4500`
- `cluster_activation_core_light` = `2.6500`
- `cluster_activation_dust_min` = `12.0000`
- `cluster_activation_dust_max` = `85.0000`
- `cluster_activation_connection_min` = `1.0000`
- `cluster_activation_connection_max` = `5.0000`
- `cluster_activation_segment_min` = `2.0000`
- `cluster_activation_segment_max` = `6.0000`

## Cluster Life

- `cluster_lifecycle_duration` = `3.2000`
- `cluster_lifecycle_birth_strength` = `1.3000`
- `cluster_lifecycle_death_strength` = `0.7200`

## Colors

- `cyan_hot` = `eaf8ff`
- `cyan_node` = `4dbdff`
- `cyan_line` = `6799ee`
- `cyan_dust` = `2f54ef`
- `halo_outer_color` = `2824f3`
- `halo_inner_color` = `acaafb`

## Structure

- `structural_node_count` = `1700.0000` requires_rebuild
- `hub_node_count` = `30.0000` requires_rebuild
- `bright_cluster_count` = `52.0000` requires_rebuild
- `structural_feature_cluster_count` = `11.0000` requires_rebuild
- `ambient_dust_node_count` = `1800.0000` requires_rebuild
- `hub_cluster_particle_count` = `56.0000` requires_rebuild
- `bright_cluster_particle_count` = `58.0000` requires_rebuild

## Shape

- `orb_radius` = `2.6500` requires_rebuild
- `display_scale` = `0.7600`
- `shape_x_scale` = `1.0000` requires_rebuild
- `shape_y_scale` = `1.0000` requires_rebuild
- `shape_z_scale` = `1.0000` requires_rebuild

## Camera

- `camera_perspective_enabled` = `on`
- `camera_fov` = `44.0000`
- `camera_distance` = `7.4000`

## Connections

- `max_connection_distance` = `0.9200` requires_rebuild
- `hub_connection_distance` = `1.2000` requires_rebuild
- `heavy_cluster_connection_chance` = `0.1000` requires_rebuild
- `heavy_cluster_connection_distance_scale` = `0.9200` requires_rebuild
- `heavy_cluster_connection_width_scale` = `1.0000` requires_rebuild

## Connection Highways

- `connection_alpha_scale` = `1.0000` requires_rebuild
- `connection_width_scale` = `1.0000` requires_rebuild
- `route_connection_chance` = `0.1600` requires_rebuild
- `route_connection_closeness_min` = `0.2400` requires_rebuild
- `route_connection_alpha_scale` = `1.0000` requires_rebuild
- `route_connection_width_scale` = `1.0000` requires_rebuild
- `route_connection_render_boost` = `0.1800` requires_rebuild
- `route_connection_glow_alpha_scale` = `1.0000` requires_rebuild
- `route_connection_core_width_scale` = `1.0000` requires_rebuild
- `route_connection_glow_width_scale` = `1.0000` requires_rebuild
- `route_connection_hot_mix` = `0.0000` requires_rebuild

## State Features

### IDLE

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### LISTENING

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### THINKING

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### SPEAKING

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### EXECUTING

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### CONFIRMATION

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

### ERROR

- `breathing` (Breathing Scale) = `on`
- `rotation` (Organic Rotation) = `on`
- `pulses` (Thinking Pulses) = `on`
- `pulse_connections` (Pulse Lines) = `on`
- `speech_motion` (Speech Motion) = `on`
- `node_motion` (Node Movement) = `on`
- `dust_motion` (Dust Movement) = `on`
- `cluster_halos` (Cluster Halos) = `on`
- `core_glow` (Core Glow) = `on`

## Cluster Overrides

- `Cluster 01` node `1715`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=146`
- `Cluster 02` node `1708`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 03` node `1702`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 04` node `1727`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 05` node `1725`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 06` node `1703`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 07` node `1729`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 08` node `1726`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 09` node `1723`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 10` node `1709`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 11` node `1716`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 12` node `1720`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 13` node `1713`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 14` node `1701`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 15` node `1717`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 16` node `1704`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 17` node `1700`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 18` node `1728`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 19` node `1711`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 20` node `1719`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 21` node `1714`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 22` node `1721`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 23` node `1710`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 24` node `1706`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 25` node `1707`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 26` node `1705`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 27` node `1712`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 28` node `1718`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 29` node `1724`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`
- `Cluster 30` node `1722`: `halo_scale=1.0000`, `brightness_scale=1.0000`, `dust_count=56`

## Connection Overrides

- None
